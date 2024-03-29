﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Serialization;
using FiLink.Models;
using ReactiveUI;

namespace FiLink.ViewModels
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        // ================================================================================
        // Private Fields 
        // ================================================================================
        
        private string _fileFolder;
        private bool _encryption;
        private string _ipRange;
        private string _hostIp;
        private string _encryptionPassword;
        private string _statusLabel;
        private string _pingTimeout;
        private bool _openFolderOnDownload;

        // ================================================================================
        // Public Fields 
        // ================================================================================
        
        public string FileFolder
        {
            get => _fileFolder;
            set
            {
                if (CheckFileFolder(value))
                {
                    StatusLabel = "";
                    this.RaiseAndSetIfChanged(ref _fileFolder, value);
                    return;
                }
                StatusLabel = "File directory path is invalid.";
            }
        }

        public bool Encryption
        {
            get => _encryption;
            set
            {
                this.RaiseAndSetIfChanged(ref _encryption, value);
                SettingsAndConstants.EnableEncryption = value;
            }
        }
        
        public bool OpenFolderOnDownload
        {
            get => _openFolderOnDownload;
            set
            {
                this.RaiseAndSetIfChanged(ref _openFolderOnDownload, value);
                SettingsAndConstants.EnableAutoOpenDownloadDir = value;
            }
        }

        public string EncryptionPassword
        {
            get => _encryptionPassword;
            set {
                if (CheckEncryptionKey(value))
                {
                    StatusLabel = "";
                    this.RaiseAndSetIfChanged(ref _encryptionPassword, value);
                    SettingsAndConstants.EncryptionPassword = value;
                    return;
                }  
                StatusLabel = "Encryption key is invalid.";
            }
        }

        public string IpRange
        {
            get => _ipRange;
            set
            {
                if (CheckIpRange(value))
                {
                    StatusLabel = "";
                    this.RaiseAndSetIfChanged(ref _ipRange, value);
                    return;
                }
                StatusLabel = "Entered IP Range is invalid";
            }
        }
        
        public string HostIp
        {
            get => _hostIp;
            set
            {
                if (CheckNewHostIp(value))
                {
                    StatusLabel = "";
                    this.RaiseAndSetIfChanged(ref _hostIp, value);
                    return;
                }  
                StatusLabel = "Entered IP address is invalid.";
            }
        }

        public string StatusLabel
        {
            get => _statusLabel;
            set => this.RaiseAndSetIfChanged(ref _statusLabel, value);
        }
        
        public string PingTimeout
        {
            get => _pingTimeout;
            set
            {
                if (int.TryParse(value, out var timeout))
                {
                    StatusLabel = "";
                    SettingsAndConstants.PingTimeout = timeout;
                    this.RaiseAndSetIfChanged(ref _pingTimeout, value);
                    return;
                }
                StatusLabel = "Ping timeout is invalid";
            }
        }

        public MainWindowViewModel ParentViewModel;
        

        // ================================================================================
        // Constructors 
        // ================================================================================
        public SettingsWindowViewModel()
        {
            IpRange = SettingsAndConstants.LowerIpAddress + "-" + SettingsAndConstants.UpperIpAddress;
            HostIp = "";
            StatusLabel = "";
            EncryptionPassword = SettingsAndConstants.EncryptionPassword;
            Encryption = SettingsAndConstants.EnableEncryption;
            OpenFolderOnDownload = SettingsAndConstants.EnableAutoOpenDownloadDir;
            FileFolder = SettingsAndConstants.FileDirectory;
            PingTimeout = SettingsAndConstants.PingTimeout.ToString();
        }
        
        // ================================================================================
        // Public Methods 
        // ================================================================================
        
        /// <summary>
        /// Applies all settings.
        /// </summary>
        public void ApplySettings()
        {
            try
            {
                if (!CheckFileFolder(FileFolder))
                {
                    StatusLabel = "Entered file directory path is incorrect.";
                    return;
                }
            
                if (!CheckNewHostIp(HostIp))
                {
                    StatusLabel = "Entered IP address is invalid.";
                    return;
                }

                if (CheckIfIpAddressExists(HostIp))
                {
                    StatusLabel = "That host address already exists.";
                    return;
                }
                ParentViewModel.HostCollection?.Add(HostIp);
            
                if (!CheckIpRange(IpRange))
                {
                    StatusLabel = "Entered IP Range is invalid";
                    return;
                }
            
                if (!CheckEncryptionKey(EncryptionPassword))
                {
                    StatusLabel = "Entered encryption key is invalid";
                    return;
                }
            
                StatusLabel = "All settings applied";
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile(e.ToString());
                StatusLabel = "Something gone wrong...";
            }
        }
        
        /// <summary>
        /// Applies all settings and saves them into a xml file.
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                ApplySettings();
                XmlSerializer xmlSerializer = new(typeof(SerializableSettings));
                TextWriter writer = new StreamWriter("settings.xml");
                xmlSerializer.Serialize(writer, SettingsAndConstants.GetSerializableSettings());
                writer.Close();
                StatusLabel = "Settings Applied & Saved.";
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile("SaveSettings : " + e);
            }
        }

        // ================================================================================
        // Private Methods
        // ================================================================================
        
        /// <summary>
        /// Checks if given IP address already exists in IP collection.
        /// </summary>
        /// <param name="ipAddress"> New IP address</param>
        /// <returns>True if address does exist, and false if it does not exist.</returns>
        /// <exception cref="Exception">Throws exception when Host IP collection (ParentViewModel.HostCollection) is a null.</exception>
        private bool CheckIfIpAddressExists(string ipAddress)
        {
            if (ipAddress == "") return false;
            if (ParentViewModel.HostCollection == null)
            {
                throw new Exception("Main Host collection is null");
            }
            return ParentViewModel.HostCollection.Any(address => ipAddress.Equals(address));
        }
        
        /// <summary>
        /// Checks the correctness of file folder path.
        /// </summary>
        /// <param name="path">The path of folder where downloaded files should be stored.</param>
        /// <returns>True if path is correct or false otherwise.</returns>
        private bool CheckFileFolder(string path)
        {
            if (path.Equals("") || path.Equals("Received_Files"))
            {
                SettingsAndConstants.FileDirectory = "Received_Files";
                return true;
            }

            if (!Directory.Exists(path)) return false;

            SettingsAndConstants.FileDirectory = path;
            return true;
        }

        /// <summary>
        /// Checks the correctness of IP address.
        /// </summary>
        /// <param name="ip">The ip to be checked.</param>
        /// <returns>True if given IP is correct or false otherwise.</returns>
        private bool CheckNewHostIp(string ip)
        {
            if (ip.Equals(""))
            {
                return true;
            }

            if (!IPAddress.TryParse(ip, out _)) return false;
            return true;
        }

        /// <summary>
        /// Checks the correctness of given IP Address range.
        /// </summary>
        /// <param name="ipRange">The IP address range in form: '[low_ip]-[up_ip]'.</param>
        /// <returns>True if IP range is correct or false otherwise.</returns>
        private bool CheckIpRange(string ipRange)
        {
            if (ipRange.Equals("") || ipRange.Equals("192.168.1.4-192.168.1.32"))
            {
                return true;
            }

            try
            {
                var ranges = ipRange.Trim().Split("-");
                var rangeLow = IPAddress.TryParse(ranges[0], out _);
                var rangeUp = IPAddress.TryParse(ranges[1], out _);

                if (!(rangeLow && rangeUp))
                {
                    throw new Exception();
                }

                SettingsAndConstants.LowerIpAddress = ranges[0];
                SettingsAndConstants.UpperIpAddress = ranges[1];

                return true;
            }
            catch (Exception)
            {
                StatusLabel = "IP ranges are incorrect";
            }

            return false;
        }

        /// <summary>
        /// Checks the correctness of encryption key.
        /// </summary>
        /// <param name="key">The encryption key to be checked.</param>
        /// <returns>True if key is correct or false otherwise.</returns>
        private bool CheckEncryptionKey(string key)
        {
            if (key is null or "")
            {
                Encryption = false;
                return true;
            }

            try
            {
                // todo add password restrictions?
                SettingsAndConstants.EncryptionPassword = key;
                return true;
            }
            catch (Exception)
            {
                StatusLabel = "Encryption key's format is incorrect";
            }

            return false;
        }
    }
}