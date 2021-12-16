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

        /// <summary>
        /// Splits large file into smaller chunks.
        /// </summary>
        /// <param name="inputFile"> Path to file.</param>
        /// <returns>Number of chunks that file has been split to.</returns>
        public static int SplitFile(string inputFile)
        {
            int chunkSize = 1024 * 1024 * 200; // 200 <- change file chunk t0 200 MiB 1,073,741,824 bytes (1 GiB)
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
        
        /// <summary>
        /// Merges chunks of file into one file.
        /// </summary>
        /// <param name="filename">Name of original file. This is used to create a pattern for searching for chunks.</param>
        public static bool MergeFile(string filename)
        {
            try
            {
                Print("[II] Merging files...");
                var separator = IsUnix() ? "/" : @"\";
                var inputDirectoryPath = SettingsAndConstants.FileDirectory + separator; // + filename + ".chunks" + separator;
                var filePattern = filename  + @".*"; 
                Thread.Sleep(50);
                
                string[] filePaths = Directory.GetFiles(inputDirectoryPath, filePattern);
                
                /*
                 *                              ========== N O T E ==========
                 * The if statement below, checks for case when there is only one part of file, ergo was already
                 * downloaded. This is important because it stops program from making a duplicate of a file,
                 * which is caused by imperfect downloading/ merging mechanism.
                 *
                 * Update - this might cause another issue - when the single downloaded file has appended part number (eg ".1")
                 * to its name, the merge method quits before removing that additional characters. Does not change or damage
                 * the file itself, but has impact on user experience. 
                 */
                if (filePaths.Length == 1)
                {
                    var file = filePaths[0];
                    if (Regex.IsMatch(file, filePattern))
                    {
                        File.Move(file, SettingsAndConstants.FileDirectory + separator + file);
                    }    
                    return false; // if there is just one file, it obviously does not need merging 
                }   
                
                
                var fileCollection = new List<string>(filePaths);
                fileCollection.Sort((s, s1) =>
                {
                    var n1 = int.Parse(s.Split(separator).Last().Split(".").Last());
                    var n2 = int.Parse(s1.Split(separator).Last().Split(".").Last());
                    return n1 >= n2 ? 1 : -1;
                });
                
                using var outputStream = File.Create(SettingsAndConstants.FileDirectory + separator + filename); // is this our problem?
                foreach (var inputFilePath in fileCollection)
                {
                    using (var inputStream = File.OpenRead(inputFilePath))
                    {
                        // Buffer size can be passed as the second argument.
                        inputStream.CopyTo(outputStream);
                    }
                    
                    if (SettingsAndConstants.EnableConsoleLog)
                        Console.WriteLine("The file {0} has been processed.", inputFilePath);
                }

                if (SettingsAndConstants.EnableConsoleLog)
                    Console.WriteLine("Files have been merged. Cleaning up...");
                
                File.Delete(filename);
                CleanupLeftoverFileChunks(filePattern, SettingsAndConstants.FileDirectory);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                LogToFile(e.ToString());
            }

            return true;
        }
        
        /// <summary>
        /// Removes file chunks left after splitting. 
        /// </summary>
        /// <param name="filePattern">Searches for chunks with this file pattern.</param>
        /// <param name="path">Path to place directory with chunks.</param>
        public static void CleanupLeftoverFileChunks(string filePattern, string path)
        {
            var leftoverFiles = GetAllFiles(filePattern, path);
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
        
        /// <summary>
        /// Gets all pattern-matching files in the directory. 
        /// </summary>
        /// <param name="filePattern">Pattern for matching files.</param>
        /// <param name="path">Path to directory potentially containing matching files.</param>
        /// <returns>Array of matched files.</returns>
        public static string[] GetAllFiles(string filePattern, string path)
        {
            var separator = IsUnix() ? "/" : @"\";
            var inputDirectoryPath = path + separator;
            string[] filePaths = Directory.GetFiles(inputDirectoryPath, filePattern);
            return filePaths;
        }

        /// <summary>
        /// Prints out messages to terminal output if such is enabled in settings.
        /// </summary>
        /// <param name="message"> Message to print out.</param>
        /// <param name="newLine"></param>
        public static void Print(string message, bool newLine = true)
        {
            if (SettingsAndConstants.EnableConsoleLog)
            {
                if (newLine) Console.WriteLine(message);
                else Console.Write(message);
            }
        }
    }
}