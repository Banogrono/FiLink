using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FiLink.Models
{
    public static class UtilityMethods
    {
        // =============================================================================================================
        // Public methods
        // =============================================================================================================

        /// <summary>
        /// Generates a key of given seed and lenght. 
        /// </summary>
        /// <param name="seed"> Seed used to generate the key. If set to 0 (default), key will be random.</param>
        /// <param name="lenght"> Lenght of the key (32 by default).</param>
        /// <returns> Key in form of string.</returns> 
        /// <exception cref="ArgumentOutOfRangeException">Lenght of the key cannot be less than or equal to 0. </exception>
        public static string GenerateKey(int seed = 0, int lenght = 32)
        {
            if (lenght <= 0) throw new ArgumentOutOfRangeException(nameof(lenght));
            StringBuilder key = new StringBuilder();
            Random random = seed == 0 ? new Random() : new Random(seed);

            for (int i = 0; i < lenght; i++)
            {
                char randomChar;
                while (true)
                {
                    var number = random.Next(48, 122);
                    if (!(number > 57 && number < 64 || number > 90 && number < 96))
                    {
                        randomChar = (char)number;
                        break;
                    }
                }

                key.Append(randomChar);
            }

            return key.ToString();
        }

        /// <summary>
        /// Removes 0x00 character form string. Useful when dealing with bytes-array-to-string conversions.
        /// </summary>
        /// <param name="s">String with problematic character.</param>
        /// <returns>String without problematic character.</returns>
        public static string RemoveNulls(string s)
        {
            StringBuilder sb = new StringBuilder();
            var ch = (char)Int16.Parse("00", NumberStyles.HexNumber);
            foreach (var c in s)
                if (c != ch)
                    sb.Append(c);

            return sb.ToString();
        }

        /// <summary>
        /// Checks if current operating system is a Unix (Linux/ MacOS). 
        /// </summary>
        /// <returns>True if is a Unix or False if it is not.</returns>
        public static bool IsUnix()
        {
            int p = (int)Environment.OSVersion.Platform;
            return (p == 4) || (p == 6) || (p == 128);
        }

        /// <summary>
        /// Logs data to file specified in SettingsAndConstants under name: LogFileName. 
        /// </summary>
        /// <param name="message">Message to be logged.</param>
        /// <param name="date">Should be there a timestamp?</param>
        public static void LogToFile(string message, bool date = true)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    if (!File.Exists("~errorLock"))
                    {
                        var fs = File.Create("~errorLock");
                        fs.Close();

                        if (date)
                        {
                            message = DateTime.Now + " : " + message;
                        }

                        TextWriter t = new StreamWriter("error.txt");
                        t.Write("\n" + message);
                        t.Close();

                        File.Delete("~errorLock");
                        break;
                    }
                    Thread.Sleep(100);
                }
            });
        }
        
         public static int SplitFile(string inputFile)
        {
            int chunkSize = 1024 * 1024 * 1024; // 1,073,741,824 bytes 
            var fileName = new FileInfo(inputFile).Name;
            const int bufferSize = 20 * 1024;
            byte[] buffer = new byte[bufferSize];

            using Stream input = File.OpenRead(inputFile);
            int index = 1;
            while (input.Position < input.Length)
            {
                using (Stream output = File.Create(fileName + "." + index))
                {
                    int remaining = chunkSize, bytesRead;
                    while (remaining > 0 && (bytesRead = input.Read(buffer, 0,
                        Math.Min(remaining, bufferSize))) > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                        remaining -= bytesRead;
                    }
                }
                index++;
                Thread.Sleep(50); // experimental; perhaps try it
            }

            return index; // number of packets
        }
        
        public static void MergeFile(string filename)
        {
            try
            {
                Console.WriteLine("Merging files...");
                var separator = IsUnix() ? "/" : @"\";
                var inputDirectoryPath = SettingsAndConstants.FileDirectory + separator; // + filename + ".chunks" + separator;
                var filePattern = filename + @".*";
            
                string[] filePaths = Directory.GetFiles(inputDirectoryPath, filePattern);
                var fileCollection = new List<string>(filePaths);
                fileCollection.Sort((s, s1) =>
                {
                    var n1 = int.Parse(s.Split(separator).Last().Split(".")[2]);
                    var n2 = int.Parse(s1.Split(separator).Last().Split(".")[2]);
                    return n1 >= n2 ? 1 : -1;
                });
                Console.WriteLine("Number of files: {0}.", filePaths.Length);
                using var outputStream = File.Create(SettingsAndConstants.FileDirectory + separator + filename);
            
                foreach (var inputFilePath in fileCollection)
                {
                    using (var inputStream = File.OpenRead(inputFilePath))
                    {
                        // Buffer size can be passed as the second argument.
                        inputStream.CopyTo(outputStream);
                    }
                    Console.WriteLine("The file {0} has been processed.", inputFilePath);
                }

                Console.WriteLine("Files have been merged. Cleaning up...");
                CleanupLeftoverFileChunks(filePattern, SettingsAndConstants.FileDirectory);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                LogToFile(e.ToString());
            }
        }

        public static void CleanupLeftoverFileChunks(string filePattern, string path)
        {
            var leftoverFiles = GetAllFiles(filePattern, path);
            Console.WriteLine(leftoverFiles.Length);
            try
            {
                Regex rg = new(@"\.[0-9]+");
                foreach (var file in leftoverFiles)
                {
                    if (rg.IsMatch(file))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                LogToFile(e.ToString());
            }
        }

        public static string[] GetAllFiles(string filePattern, string path)
        {
            var separator = IsUnix() ? "/" : @"\";
            var inputDirectoryPath = path + separator; 
            string[] filePaths = Directory.GetFiles(inputDirectoryPath, filePattern);
            return filePaths;
        }
    }
}