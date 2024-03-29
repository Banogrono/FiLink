﻿namespace FiLink.Models
{
    public class SerializableSettings
    {
        // =============================================================================================================
        // Public Fields
        // =============================================================================================================
        public string LowerIpAddress { get; set; }
        public string UpperIpAddress { get; set; }
        public string FileDirectory { get; set; }
        public string LogFileName;
        public string SavedHostsFileName;

        public int PingTimeout { get; set; }

        // =============================================================================================================
        // Public Methods
        // =============================================================================================================

        /// <summary>
        /// This method serializes settings into a xml file.
        /// </summary>
        /// <param name="logFileName"> The name of the file containing logs written on runtime. </param>
        /// <param name="savedHostsFileName"> The name of the file containing host names. </param>
        /// <param name="lowerIpAddress">The IP from which the scanning starts.</param>
        /// <param name="upperIpAddress">The IP on which the scanning ends.</param>
        /// <param name="fileDirectory">The file directory with downloaded files.</param>
        /// <param name="pingTimeout">The ping time out (128 ms by default).</param>
        public SerializableSettings(string logFileName, string savedHostsFileName,
            string lowerIpAddress, string upperIpAddress, string fileDirectory, int pingTimeout)
        {
            LogFileName = logFileName;
            SavedHostsFileName = savedHostsFileName;
            LowerIpAddress = lowerIpAddress;
            UpperIpAddress = upperIpAddress;
            FileDirectory = fileDirectory;
            PingTimeout = pingTimeout;
        }

        public SerializableSettings()
        {
            LogFileName = SettingsAndConstants.LogFileName;
            SavedHostsFileName = SettingsAndConstants.SavedHostsFileName;
            LowerIpAddress = SettingsAndConstants.LowerIpAddress;
            UpperIpAddress = SettingsAndConstants.UpperIpAddress;
            FileDirectory = SettingsAndConstants.FileDirectory;
            PingTimeout = SettingsAndConstants.PingTimeout;
        }
    }
}