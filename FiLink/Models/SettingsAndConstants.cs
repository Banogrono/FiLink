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
            OnDirectoryChanged(null, null!);
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
        
        public static readonly string Help =   "Welcome to FiLink File Transfer Application!\n" +
                                               "Available arguments: \n" +
                                               "[--help | -h] - opens this help.\n" +
                                               "[--cli ] - launches application without GUI in text interface mode.\n" +
                                               "[--faf <ip> <path_to_file> ] - (fire and forget) sends a file and closes application.\n" +
                                               "[--quiet | -q] - disables most console logging.\n" +
                                               "[--noserver | -ns] - disables server. Warning: application cannot receive files in this mode!\n" +
                                               "[--nofinder | -nf] - disables Host Finder. Warning: hosts can not be searched for/ found in this mode!\n" +
                                               "[--ips <ip>... | -a] - add IP addresses of hosts, each address has to be separated by space. Used without arguments shows list of entered IPs.\n" +
                                               "[--files <path_to_file>... | -f] - add files, each path to file has to be separated with space. Names containing spaces have to be inside \"\". Used without arguments shows list of entered files.  \n" +
                                               "[--send | -s] - sends all entered files to all entered hosts.\n" +
                                               "[--clearFiles | -cf] - clears files list.\n" +
                                               "[--clearHost | -ch ] - clears host list.\n" +
                                               "[--exit | -e ] - closes the application.\n";

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