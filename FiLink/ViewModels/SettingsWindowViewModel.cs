using System;
using System.IO;
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
        private string _encryptionKey;
        private string _statusLabel;
        private string _pingTimeout;

        // ================================================================================
        // Public Fields 
        // ================================================================================
        
        public string FileFolder
        {
            get => _fileFolder;
            set => this.RaiseAndSetIfChanged(ref _fileFolder, value);
        }

        public bool Encryption
        {
            get => _encryption;
            set => this.RaiseAndSetIfChanged(ref _encryption, value);
        }

        public string EncryptionKey
        {
            get => _encryptionKey;
            set => this.RaiseAndSetIfChanged(ref _encryptionKey, value);
        }

        public string IpRange
        {
            get => _ipRange;
            set => this.RaiseAndSetIfChanged(ref _ipRange, value);
        }

        public string HostIp
        {
            get => _hostIp;
            set => this.RaiseAndSetIfChanged(ref _hostIp, value);
        }

        public string StatusLabel
        {
            get => _statusLabel;
            set => this.RaiseAndSetIfChanged(ref _statusLabel, value);
        }

        // todo: think about such implementation for other properties 
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
            EncryptionKey = "156156";
            Encryption = false;
            FileFolder = "Received_Files";
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
            var dirOk = CheckFileFolder(FileFolder);
            if (!dirOk)
            {
                StatusLabel = "File directory path is incorrect.";
                return;
            }

            var newHostOk = CheckNewHostIp(HostIp);
            if (!newHostOk)
            {
                StatusLabel = "Host IP is incorrect.";
                return;
            }

            if (!CheckIpRange(IpRange))
            {
                return;
            }

            if (CheckEncryptionKey(EncryptionKey))
            {
                return;
            }

            StatusLabel = "All settings applied";
        }
        
        /// <summary>
        /// Applies all settings and saves them into a xml file.
        /// </summary>
        public void SaveSettings()
        {
            ApplySettings();
            try
            {
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
            ParentViewModel.HostCollection?.Add(ip);
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
            if (key.Equals(""))
            {
                return true;
            }

            try
            {
                var enKey = int.Parse(key);
                SettingsAndConstants.EncryptionKey = enKey;
            }
            catch (Exception)
            {
                StatusLabel = "Encryption key's format is incorrect";
            }

            return false;
        }
    }
}