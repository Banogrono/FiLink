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
        // Fields
        // =============================================================================================================
        private const int Port = 4400;
        private string _directory;

        private readonly TcpListener _infoListener, _dataListener;
        private TcpClient _infoChannel, _dataChannel;
        private NetworkStream _infoStream;
        private string _sessionKey;

        public bool EncryptionEnabled = false;
        private int _encryptionKey = 696969;

        public static bool EnableConsoleLog { get; set; } = SettingsAndConstants.EnableConsoleLog;

        // =============================================================================================================
        // Constructors
        // =============================================================================================================
        public Server(string dataDirectory = "Received_Files")
        {
            _infoListener = new TcpListener(IPAddress.Any, Port - 2);
            _dataListener = new TcpListener(IPAddress.Any, Port);

            _infoListener.Start();
            _dataListener.Start();

            _directory = dataDirectory;
            _sessionKey = null!;

            Connect();
        }

        public Server(ref TcpClient infoChannel, ref TcpClient dataChannel, string dataDirectory = "Received_Files")
        {
            _dataChannel = dataChannel;
            _infoChannel = infoChannel;
            _infoStream = _infoChannel.GetStream();
            NegotiateSessionKey();

            _directory = dataDirectory;
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
                    GetFile(savingPath, fileSize);
                    OnFileReceived?.Invoke(this, EventArgs.Empty);
                }

                var targetFileName = fileName.Remove(fileName.LastIndexOf(".", StringComparison.Ordinal));
                UtilityMethods.MergeFile(targetFileName); // merging file chunks

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

        /// <summary>
        /// Sets encryption key for encrypting files and info streams.
        /// </summary>
        /// <param name="key">Encryption key.</param>
        public void SetEncryptionKey(int key)
        {
            _encryptionKey = key;
        }

        /// <summary>
        /// Sets save directory for downloaded files. 
        /// </summary>
        /// <param name="path">Path of directory.</param>
        /// <exception cref="Exception">Throws exception when given path doesnt exist.</exception>
        public void SetSaveDirectory(string path)
        {
            if (!Directory.Exists(path)) throw new Exception("Directory does not exist.");

            _directory = path;
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

                    OnDownloadProgress?.Invoke(this, EventArgs.Empty);
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
                    var decryptedCallback = Encryption.Decrypt(encodedCallback, _encryptionKey);
                    decodedCallback = UtilityMethods.RemoveNulls(Encoding.UTF8.GetString(decryptedCallback));
                }
                else
                {
                    decodedCallback = UtilityMethods.RemoveNulls(Encoding.UTF8.GetString(encodedCallback));
                }

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
            while (true)
            {
                var receivedKey = ReceiveCallback();
                if (!receivedKey.Contains("set_key:")) continue;

                var key = receivedKey.Split(":")[1];
                var isKeyValid = CheckSessionKey(key);

                if (!isKeyValid) continue;
                SendInformation("key_accept:" + key);

                var response = ReceiveCallback();
                if (response.Equals("key_established:" + key)) return;
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
        public EventHandler OnFileReceived;

        /// <summary>
        /// Invoked when session with client is closed.
        /// </summary>
        public EventHandler OnSessionClosed;

        /// <summary>
        /// Invoked on download progress of the file. 
        /// </summary>
        public EventHandler OnDownloadProgress;
    }
}