using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FiLink.Models
{
    public class Server
    {
        // =============================================================================================================
        // Private Fields
        // =============================================================================================================
        private const int Port = 4400;
        private string _directory;

        private readonly TcpListener _infoListener, _dataListener;
        private TcpClient _infoChannel, _dataChannel;
        private NetworkStream _infoStream;
        private string _sessionKey;
        private int _encryptionKey;
        
        // =============================================================================================================
        // Public Fields
        // =============================================================================================================
        public bool EncryptionEnabled = SettingsAndConstants.EnableEncryption;
        public static bool EnableConsoleLog { get; set; } = SettingsAndConstants.EnableConsoleLog;

        // =============================================================================================================
        // Constructors
        // =============================================================================================================
        
        public Server(ref TcpClient infoChannel, ref TcpClient dataChannel, string dataDirectory = "Received_Files") // todo: before removing check TUI interface - this param might be used there
        {
            _dataChannel = dataChannel;
            _infoChannel = infoChannel;
            _infoStream = _infoChannel.GetStream();
            NegotiateSessionKey();

            _directory = SettingsAndConstants.FileDirectory;
            _encryptionKey = SettingsAndConstants.EncryptionKey;

            SettingsAndConstants.OnDirectoryChanged += SetSaveDirectory;
        }

        // =============================================================================================================
        // Public Methods
        // =============================================================================================================

        /// <summary>
        /// Receives files and saves them into given directory. 
        /// </summary>
        public void Receive()
        {
            try
            {
                if (EnableConsoleLog) Console.WriteLine("Client Connected");
                if (!Directory.Exists(_directory)) Directory.CreateDirectory(_directory);
                string fileName = "";
                while (true)
                {
                    SendInformation("ready");

                    var response = ReceiveCallback();
                    if (EnableConsoleLog) Console.WriteLine(response);
                    if (response == null) return;

                    if (response.Contains("server_stop:" + _sessionKey)) break;

                    var fileInfo = response.Split(":");

                    if (fileInfo.Length != 3) return;

                    fileName = fileInfo[1];
                    var fileSize = int.Parse(fileInfo[2]);
                    if (fileSize == 0) return;

                    if (EnableConsoleLog) Console.WriteLine("receiving: " + fileName);
                    var savingPath = UtilityMethods.IsUnix() ? $"{_directory}/{fileName}" : $@"{_directory}\{fileName}";
                    
                    // this saves file and caused duplicates when file was small enough to fit in buffer. Then the
                    // merging method tried to "merge" that one small file (file that had just one part) and just copied 
                    // it, making a duplicate.
                    GetFile(savingPath, fileSize);  
                    OnFileReceived?.Invoke(this, EventArgs.Empty);
                }

                // merging file chunks
                UtilityMethods.MergeFile(fileName.Remove(fileName.LastIndexOf(".", StringComparison.Ordinal))); 

                Close();
                if (EnableConsoleLog) Console.WriteLine("server out");
                OnSessionClosed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception e)
            {
                if (EnableConsoleLog) Console.WriteLine(e);
                UtilityMethods.LogToFile(e.ToString());
                throw;
            }
        }
        
        // =============================================================================================================
        // Private Methods
        // =============================================================================================================

        /// <summary>
        /// Connects with client.
        /// </summary>
        private void Connect()
        {
            _infoChannel = _infoListener.AcceptTcpClient();
            _dataChannel = _dataListener.AcceptTcpClient();
            _infoStream = _infoChannel.GetStream();
            NegotiateSessionKey();
            _infoListener.Stop();
            _dataListener.Stop();
        }
        
        /// <summary>
        /// Sets new directory where downloaded files will be saved. Invoked via event.
        /// </summary>
        private void SetSaveDirectory(object? sender, EventArgs eventArgs)
        {
            _directory = SettingsAndConstants.FileDirectory;
        }

        /// <summary>
        /// Downloads the file from the client and saves it.
        /// </summary>
        /// <param name="fileName">Name of the file to save.</param>
        /// <param name="fileSize">Size of the file to download.</param>
        /// <exception cref="Exception">Throws exception when session keys don't match.</exception>
        private void GetFile(string fileName, int fileSize)
        {
            var receiveSocket = _dataChannel.Client;
            var dataReceived = 0;
            var dataLeft = fileSize;
            var data = new byte[fileSize];

            var sessionKeyEncoded = new byte[32];
            receiveSocket.Receive(sessionKeyEncoded, 0, 32, SocketFlags.None);
            var sessionKeyDecoded = Encoding.UTF8.GetString(EncryptionEnabled
                ? Encryption.Decrypt(sessionKeyEncoded, _encryptionKey)
                : sessionKeyEncoded);

            if (_sessionKey != sessionKeyDecoded)
            {
                if (EnableConsoleLog) Console.WriteLine(_sessionKey + " " + sessionKeyDecoded);
                throw new Exception("E: SESSION KEY DOES NOT MATCH");
            }

            try
            {
                using var fileStream = new FileStream(fileName, FileMode.Create);
                while (dataReceived < fileSize)
                {
                    var bytesReceived = receiveSocket.Receive(data, dataReceived, dataLeft, SocketFlags.None);
                    fileStream.Write(data, dataReceived, bytesReceived);

                    if (bytesReceived == 0) break;

                    dataReceived += bytesReceived;
                    dataLeft -= bytesReceived;

                    int[] progress = { dataReceived, fileSize };
                    OnDownloadProgress?.Invoke(this, progress);
                }
            }
            catch (Exception e)
            {
                if (EnableConsoleLog) Console.WriteLine(e);
                UtilityMethods.LogToFile(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// Sends string information (on port 4398 by default). 
        /// </summary>
        /// <param name="information">A string to be sent.</param>
        private void SendInformation(string information)
        {
            try
            {
                if (!_infoChannel.Connected) return;
                var encodedInformation = Encoding.UTF8.GetBytes(information);
                if (EncryptionEnabled)
                {
                    var encryptedEncodedInformation = Encryption.Encrypt(encodedInformation, _encryptionKey);
                    _infoStream.Write(encryptedEncodedInformation, 0, encryptedEncodedInformation.Length);
                    _infoStream.Flush();
                    // todo remove
                    Console.WriteLine("S S: ");
                    foreach (var b in encryptedEncodedInformation)
                    {
                        Console.Write(b);
                    }
                    Console.WriteLine();
                    return;
                }

                _infoStream.Write(encodedInformation, 0, encodedInformation.Length);
                _infoStream.Flush();
            }
            catch (Exception e)
            {
                if (EnableConsoleLog) Console.WriteLine(e);
                UtilityMethods.LogToFile(e.ToString());
            }
        }

        /// <summary>
        /// Listens for data on port 4398 (information channel). Used for information exchange. This method D O E S  N O T receive files.  
        /// </summary>
        /// <returns>Received data in form of string.</returns>
        private string ReceiveCallback()
        {
            try
            {
                if (!_infoChannel.Connected) return null!;
                var encodedCallback = new byte[_infoChannel.ReceiveBufferSize];
                _infoStream.Read(encodedCallback, 0, encodedCallback.Length);
                string decodedCallback;
                if (EncryptionEnabled)
                {
                    // todo remove
                    Console.WriteLine("S R: ");
                    foreach (var b in encodedCallback)
                    {
                        Console.Write(b);
                    }
                    Console.WriteLine();
                    
                    var decryptedCallback = Encryption.Decrypt(encodedCallback, _encryptionKey);
                    Console.WriteLine("S DEC: ");
                    foreach (var b in decryptedCallback)
                    {
                        Console.Write(b);
                    }
                    Console.WriteLine();
                    decodedCallback = UtilityMethods.RemoveNulls(Encoding.UTF8.GetString(decryptedCallback));
                }
                else
                {
                    decodedCallback = UtilityMethods.RemoveNulls(Encoding.UTF8.GetString(encodedCallback));
                }
                
                Console.WriteLine(decodedCallback);
                Console.WriteLine();
                return decodedCallback;
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("An existing connection was forcibly closed by the remote host"))
                    return null!;
                else
                    UtilityMethods.LogToFile(e.ToString());
                return null!;
            }
        }

        /// <summary>
        /// Negotiates sessions key with client - additional handshake.
        /// </summary>
        private void NegotiateSessionKey()
        {
            try
            {
                while (true)
                {
                    var receivedKey = ReceiveCallback();
                    if (receivedKey == null) throw new Exception("NegotiateSessionKey: Key cannot be a null.");
                
                    if (!receivedKey.Contains("set_key:")) continue;

                    var key = receivedKey.Split(":")[1];
                    var isKeyValid = CheckSessionKey(key);

                    if (!isKeyValid) continue;
                    SendInformation("key_accept:" + key);

                    var response = ReceiveCallback();
                    if (response.Equals("key_established:" + key)) return;
                }
            }
            catch (Exception e)
            {
                if (EnableConsoleLog) Console.WriteLine(e.Message);
                UtilityMethods.LogToFile(e.ToString());
            }
        }

        /// <summary>
        /// Checks if session key already exists.
        /// </summary>
        /// <param name="key">session key.</param>
        /// <returns>If session key is valid, then return true, else return false.</returns>
        private bool CheckSessionKey(string key)
        {
            var keyValid = SettingsAndConstants.SessionKeys.All(savedKey => savedKey != key);

            if (keyValid)
            {
                _sessionKey = key;
                SettingsAndConstants.SessionKeys.Add(key);
            }

            return keyValid;
        }

        /// <summary>
        /// Peacefully closes the server. 
        /// </summary>
        private void Close()
        {
            _infoStream.Close();
            _dataChannel.Close();
            _infoChannel.Close();
        }

        // =============================================================================================================
        // Events 
        // =============================================================================================================

        /// <summary>
        /// Invoked when file is downloaded.
        /// </summary>
        public static event EventHandler OnFileReceived;

        /// <summary>
        /// Invoked when session with client is closed.
        /// </summary>
        public static event EventHandler OnSessionClosed;

        /// <summary>
        /// Invoked on download progress of the file. First number in the array contains received parts,
        /// and the second total number of file parts.
        /// </summary>
        public static event EventHandler<int[]> OnDownloadProgress;
    }
}