using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace FiLink.Models
{
    [Serializable]
    public static class SettingsAndConstants
    {
        // =============================================================================================================
        //  Public Fields 
        // =============================================================================================================
        public static string LowerIpAddress { get; set; }
        public static string UpperIpAddress { get; set; }

        public static string FileDirectory
        {
            get => _fileDirectory;
            set {
            _fileDirectory = value;
            OnDirectoryChanged?.Invoke(null, null!);
            }
        }

        public static readonly string LogFileName;
        public static readonly string SavedHostsFileName;
        public static int PingTimeout { get; set; }
        public static string EncryptionPassword { get; set; }
        public static bool EnableEncryption { get; set; }

        public static List<string> SessionKeys;
        private static string _fileDirectory;

        public static string TempFilesDir { get; set; }

        public static bool EnableConsoleMode { get; set; }

        public static bool EnableServer { get; set; } = true;

        public static bool EnableHostFinder { get; set; } = true;

        public static bool EnableConsoleLog { get; set; } = true;
        
        public static bool EnableAutoOpenDownloadDir { get; set; } = true;

        // =============================================================================================================
        // Constructors 
        // =============================================================================================================
        static SettingsAndConstants()
        {
            if (File.Exists("settings.xml"))
            {
                var settings = LoadSerializableSettings();
                LowerIpAddress = settings.LowerIpAddress;
                UpperIpAddress = settings.UpperIpAddress;
                FileDirectory = settings.FileDirectory;
                LogFileName = settings.LogFileName;
                SavedHostsFileName = settings.SavedHostsFileName;
                PingTimeout = settings.PingTimeout;
                SessionKeys = new List<string>();
                TempFilesDir = "temp";
                EnableEncryption = false;
                
                // todo remove that later
                EncryptionPassword = "152152";
            }
            else
            {
                LowerIpAddress = "192.168.1.6";
                UpperIpAddress = "192.168.1.30";
                FileDirectory = "Received_Files";
                LogFileName = "ErrorLog.txt";
                SavedHostsFileName = "hosts.xml";
                PingTimeout = 128;
                SessionKeys = new List<string>();
                TempFilesDir = "temp";
                EnableEncryption = false;
                
                // todo remove that later
                EncryptionPassword = "152152";
            }
        }

        // =============================================================================================================
        // Public Methods 
        // =============================================================================================================

        /// <summary>
        /// Constructs new SerializableSettings object based on data from this class.
        /// </summary>
        /// <returns>SerializableSettings object with data.</returns>
        public static SerializableSettings GetSerializableSettings()
        {
            return new SerializableSettings(LogFileName, SavedHostsFileName, LowerIpAddress,
                UpperIpAddress, FileDirectory, PingTimeout);
        }

        // =============================================================================================================
        // Private Methods 
        // =============================================================================================================

        /// <summary>
        /// Loads and deserializes SerializableSettings object from disk. Used to initialize this class.
        /// </summary>
        /// <returns>SerializableSettings object with data.</returns>
        private static SerializableSettings LoadSerializableSettings()
        {
            try
            {
                XmlSerializer xmlSerializer = new(typeof(SerializableSettings));
                using Stream stream = new FileStream("settings.xml", FileMode.Open);

                var settings = xmlSerializer.Deserialize(stream) as SerializableSettings;

                return settings!;
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile("LoadSerializableSettings : " + e);
                throw;
            }
        }
        
        // =============================================================================================================
        // Events 
        // =============================================================================================================

        public static event EventHandler OnDirectoryChanged;

    }
}