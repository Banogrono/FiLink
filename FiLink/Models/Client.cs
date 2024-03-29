﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiLink.Models
{
    public class Client
    {
        // =============================================================================================================
        // Private Fields
        // =============================================================================================================
        private const int Port = 4400;
        private readonly string _ip;
        private TcpClient _infoChannel;
        private NetworkStream _infoStream = null!;
        private TcpClient _dataChannel;
        private string _sessionKey;

        // =============================================================================================================
        // Public Fields
        // =============================================================================================================

        public bool EncryptionEnabled;

        // =============================================================================================================
        // Constructors
        // =============================================================================================================
        public Client(string ip)
        {
            _ip = ip;
            _infoChannel = new TcpClient();
            _dataChannel = new TcpClient();
            _sessionKey = UtilityMethods.GenerateKey();
            EncryptionEnabled = SettingsAndConstants.EnableEncryption;
        }

        // =============================================================================================================
        // Public Methods
        // =============================================================================================================

        /// <summary>
        /// Sends a file. 
        /// </summary>
        /// <param name="filepath">File path to the file that should be sent.</param>
        public void Send(string filepath)
        {
            try
            {
                if (EncryptionEnabled)
                {
                    UtilityMethods.Print("[II] Encrypting file");
                    Encryption.FileEncrypt(filepath, SettingsAndConstants.EncryptionPassword);
                    filepath = filepath + ".aes";
                }
            
                if (new FileInfo(filepath).Length > 200 * 1024 * 1024) // 200 MiB?
                {
                    UtilityMethods.SplitFile(filepath);
                    var filename = new FileInfo(filepath).Name;
                    var filePattern = filename + ".*";
                    string[] filePaths = Directory.GetFiles(Directory.GetCurrentDirectory(), filePattern);

                    foreach (var fileChunk in filePaths)
                    {
                        UtilityMethods.Print("[II] Sending: " + fileChunk);
                        EstablishConnectionAndSendFile(fileChunk);
                        Thread.Sleep(50);
                        OnChunkSent?.Invoke(this, filePaths.Length);
                    }

                    UtilityMethods.CleanupLeftoverFileChunks(filePattern, Directory.GetCurrentDirectory());
                }
                else
                {
                    EstablishConnectionAndSendFile(filepath);
                }
            
                // delete encrypted file 
                if (EncryptionEnabled)
                {
                    File.Delete(filepath);
                }
                
                var response = ReceiveCallback();
                if (!response.Contains("ready")) return;
                SendInformation("server_stop:" + _sessionKey);
                Thread.Sleep(50);
                Close();
                OnDataSent?.Invoke(null, null!); // possible uses...
            }
            catch (Exception e)
            {
                UtilityMethods.Print("[EE] " + e.Message);
                UtilityMethods.LogToFile("[EE] " + e);
            }
        }

        // =============================================================================================================
        // Private Methods
        // =============================================================================================================

        /// <summary>
        /// Establishes connection with server and sends file.
        /// </summary>
        /// <param name="filepath">Path to file.</param>
        private void EstablishConnectionAndSendFile(string filepath)
        {
            try
            {
                if (!(_infoChannel.Connected && _dataChannel.Connected))
                {
                   Connect();
                }

                var response = ReceiveCallback();

                if (!response.Contains("ready")) return;

                var fileInfo = "put_data:" + GetFileInfo(filepath);
                SendInformation(fileInfo);
                SendData(filepath);
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile(e.ToString());
                OnClientUnreachable?.Invoke(null, null!);
                throw;
            }
        }

        /// <summary>
        /// Connects to server. Operates on ports 4400 (file transmission) and 4398 [4400 - 2] (info transmission) by default.
        /// </summary>
        /// 
        private void Connect(int connectionTimeout = 5)
        {
            try
            {
                _infoChannel = new TcpClient();
                _dataChannel = new TcpClient();
                
                UtilityMethods.Print("[II] Connecting...");
                // connecting infoChannel
                var connectionResult = _infoChannel.BeginConnect(_ip, Port - 2, null, null);
                var success = connectionResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(connectionTimeout));
                if (!success)
                {
                    throw new Exception("Failed to connect [info link].");
                }
                _infoChannel.EndConnect(connectionResult);
                
                // connecting dataChannel
                connectionResult = _dataChannel.BeginConnect(_ip, Port, null, null);
                success = connectionResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(connectionTimeout));
                if (!success)
                {
                    throw new Exception("Failed to connect [data link].");
                }
                _dataChannel.EndConnect(connectionResult);
                UtilityMethods.Print("[II] Connection successful");
                
                _infoStream = _infoChannel.GetStream();
                NegotiateSessionKey();
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// Safely closes the client session. 
        /// </summary>
        private void Close()
        {
            Task.Delay(500);
            UtilityMethods.Print("[II] Client offline");
            _infoChannel.Close();
            _dataChannel.Close();
            OnClientClosed?.Invoke(this, null!);
        }

        /// <summary>
        /// Gets information such as file name and size for given file and turns it into a string. 
        /// </summary>
        /// <param name="path">Path to file</param>
        /// <returns>String with file name and size joined by ':' character (see protocol for more information).</returns>
        private string GetFileInfo(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    StringBuilder sb = new StringBuilder();
                    sb.Append(fi.Name).Append(":").Append(fi.Length);
                    return sb.ToString();
                }
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile(e.ToString());
            }

            return null!;
        }

        /// <summary>
        /// Sends string information (on port 4398 by default). 
        /// </summary>
        /// <param name="information">A string to be sent.</param>
        private void SendInformation(string information)
        {
            try
            {
                var encodedInformation = Encoding.UTF8.GetBytes(information);
                _infoStream.Write(encodedInformation, 0, encodedInformation.Length);
                _infoStream.Flush();
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile(e.ToString());
            }
        }

        /// <summary>
        /// Sends the files up to 2 GB in size (on port 4400 by default).
        /// </summary>
        /// <param name="filename">The path to the file to be sent.</param>
        private void SendData(string filename)
        {
            try
            {
                var dataSocket = _dataChannel.Client;
                var sessionKeyEncoded = Encoding.UTF8.GetBytes(_sessionKey);
                dataSocket.SendFile(filename, sessionKeyEncoded, new byte[] { },
                    TransmitFileOptions.UseDefaultWorkerThread);
                // todo find a way to encrypt a file before sending 

                OnDataSent?.Invoke(this, null!);
            }
            catch (Exception e)
            {
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
                var encodedCallback = new byte[_infoChannel.ReceiveBufferSize];
                _infoStream.Read(encodedCallback, 0, encodedCallback.Length);
                var decodedCallback = UtilityMethods.RemoveNulls(Encoding.UTF8.GetString(encodedCallback));
                return decodedCallback;
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile(e.ToString());
                return null!;
            }
        }

        /// <summary>
        /// Negotiates sessions key with server - additional handshake.
        /// </summary>
        private void NegotiateSessionKey()
        {
            UtilityMethods.Print("[II] Negotiating session keys...");
            while (true)
            {
                var setKey = "set_key:" + _sessionKey;
                SendInformation(setKey);
                var response = ReceiveCallback();
                if (response.Equals("key_accept:" + _sessionKey))
                {
                    SendInformation("key_established:" + _sessionKey);
                    return; // key set
                }

                // generate new session key in case one generated before was rejected. 
                _sessionKey = UtilityMethods.GenerateKey();
            }
        }

        // =============================================================================================================
        // Events
        // =============================================================================================================

        /// <summary>
        /// Event Invoked when Client closes its session.
        /// </summary>
        public static EventHandler? OnClientClosed;

        /// <summary>
        /// Event Invoked when Client has sent all data.
        /// </summary>
        public static  EventHandler? OnDataSent;
        
        /// <summary>
        /// Event Invoked when cannot connect with client (timeout, host not valid etc.).
        /// </summary>
        public static  EventHandler? OnClientUnreachable;

        /// <summary>
        /// Event Invoked when Client has sent a chunk of data.
        /// </summary>
        public static EventHandler<int>? OnChunkSent;
    }
}