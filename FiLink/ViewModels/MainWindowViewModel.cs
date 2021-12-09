using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Controls;
using FiLink.Models;
using FiLink.Views;
using ReactiveUI;

namespace FiLink.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        // ================================================================================
        // Private Fields 
        // ================================================================================

        private string _infoLabel;
        private float _progressBarValue;
        private int _chunksSent;
        private int _chunksToBeSent;
        private bool _directoryLock;

        // ================================================================================
        // Public Fields 
        // ================================================================================
        public ObservableCollection<string>? HostCollection { get; private set; }
        public ObservableCollection<string> FileCollection { get; }

        public ObservableCollection<string> SelectedHosts { get; set; }
        public ObservableCollection<string> SelectedFiles { get; set; }
        public Window ThisWindow { get; set; }

        public float ProgressBarValue
        {
            get => _progressBarValue;
            set => this.RaiseAndSetIfChanged(ref _progressBarValue, value);
        }

        public string InfoLabel
        {
            get => _infoLabel;
            set => this.RaiseAndSetIfChanged(ref _infoLabel, value);
        }

        // ================================================================================
        // Constructors 
        // ================================================================================
        public MainWindowViewModel()
        {
            _infoLabel = "";
            ProgressBarValue = 0;
            HostCollection = LoadHosts() ?? new ObservableCollection<string>();
            FileCollection = new ObservableCollection<string>();

            SelectedFiles = new ObservableCollection<string>();
            SelectedHosts = new ObservableCollection<string>();
            
            _directoryLock = false;
            
            // HostFinder.OnHostSearchProgressed += ChangeProgressBarValue; // todo: find a better way of doing that
            Encryption.OnDecryptingFile += OnDecryption;
            Encryption.OnEncryptingFile += OnEncryption;
            
            Server.OnFileReceived += OnFileDownloaded;
            Server.OnDownloadProgress += OnDownloadProgress;

            Client.OnClientUnreachable += (_, _) => { InfoLabel = "Client unreachable";};
            Client.OnDataSent += (_, _) => { InfoLabel = "Data sent!";};
            Client.OnChunkSent += OnChunkSent;
        }

        // ================================================================================
        // Public Methods 
        // ================================================================================

        /// <summary>
        /// Sends files asynchronously. 
        /// </summary>
        public async void SendFiles()
        {
            if (SelectedHosts.Count == 0)
            {
                InfoLabel = "E: No hosts selected.";
                return;
            }

            if (SelectedFiles.Count == 0)
            {
                InfoLabel = "E: No files selected.";
                return;
            }

            try
            {
                InfoLabel = "Sending files...";
                await Task.Run(() =>
                {
                    ProgressBarValue = 0;
                    var step = 100.0F / (SelectedHosts.Count * SelectedFiles.Count);
                    foreach (var host in SelectedHosts)
                    {
                        string ip;
                        if (host.Contains(":"))
                        {
                            ip = host.Split(":")[1];
                        }
                        else
                        {
                            ip = host;
                        }

                        if (!IPAddress.TryParse(ip, out _))
                        {
                            throw new Exception("IP cannot be parsed");
                        }

                        foreach (var file in SelectedFiles)
                        {
                            try
                            {
                                Client client = new(ip);
                                InfoLabel = "Sending " + Path.GetFileName(file);
                                client.Send(@"" + file);
                            }
                            catch (Exception e)
                            {
                                UtilityMethods.LogToFile("SendFilesAsync : " + e);
                            }

                            ProgressBarValue += step;
                        }
                    }
                });
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile("SendFilesAsync : " + e);
            }
        }

        /// <summary>
        /// Saves selected hosts on to a disk in a form of .xml file.
        /// </summary>
        public void SaveHosts()
        {
            if (SelectedHosts.Count == 0)
            {
                InfoLabel = "E: No hosts selected.";
                return;
            }

            try
            {
                XmlSerializer xmlSerializer = new(typeof(ObservableCollection<string>));
                TextWriter writer = new StreamWriter(SettingsAndConstants.SavedHostsFileName);
                xmlSerializer.Serialize(writer, SelectedHosts);
                writer.Close();
                InfoLabel = "Hosts saved.";
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile("SaveHosts : " + e);
            }
        }

        /// <summary>
        /// Removes saved hosts file
        /// </summary>
        public void RemoveHosts()
        {
            try
            {
                if (!File.Exists(SettingsAndConstants.SavedHostsFileName))
                {
                    InfoLabel = "There are no hosts to remove";
                    return;
                }

                File.Delete(SettingsAndConstants.SavedHostsFileName);
                InfoLabel = "Hosts removed";
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile("RemoveHosts : " + e);
            }
        }

        /// <summary>
        /// Updates the HostCollection with new-found hosts.
        /// </summary>
        public async void RefreshHosts()
        {
            InfoLabel = "Refreshing hosts...";
            List<string> availableHosts = await Task.Run(() =>
                HostsGetter(SettingsAndConstants.LowerIpAddress, SettingsAndConstants.UpperIpAddress));
            HostCollection ??= new ObservableCollection<string>();
            HostCollection.Clear();
            foreach (var host in availableHosts)
            {
                HostCollection.Add(host);
            }
        }

        /// <summary>
        /// Opens File Explorer (on Windows) or a Krusader (on Linux). 
        /// </summary>
        public void OpenFolder()
        {
            var linux = UtilityMethods.IsUnix();
            var dir = Directory.GetCurrentDirectory() + (linux ? "/" : @"\") + SettingsAndConstants.FileDirectory;
            if (Directory.Exists(dir))
            {
                string fileExplorer = UtilityMethods.IsUnix() ? "thunar" : "explorer.exe"; // todo add more generic way to call FE on linux
                ProcessStartInfo startInfo = new()
                {
                    Arguments = dir,
                    FileName = fileExplorer,
                };
                
                var directoryDialog = Process.Start(startInfo);
                Task.Run(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(100);
                        if (directoryDialog is { HasExited: true })
                        {
                            InfoLabel = "lock lifted ";
                            
                            break;
                        }
                    }
                });
            }
            else
            {
                InfoLabel = "Directory does not exist.";
            }
        }

        /// <summary>
        /// Opens settings window.
        /// </summary>
        public void OpenSettingsWindow()
        {
            var settingsWindowController = new SettingsWindowViewModel()
            {
                ParentViewModel = this
            };
            var settingsWindow = new SettingsWindow()
            {
                DataContext = settingsWindowController,
                ViewModel = settingsWindowController
            };
            
            settingsWindow.ShowDialog(ThisWindow);
        }

        // ================================================================================
        // Private Methods 
        // ================================================================================
        
        /// <summary>
        /// Searches for hosts in LAN and returns what it has found.
        /// </summary>
        /// <param name="lowIp">Search starts from this address.</param>
        /// <param name="uppIp">Search ends upon hitting this address.</param>
        /// <returns>List of found hosts.</returns>
        private List<string> HostsGetter(string lowIp = "192.168.1.5", string uppIp = "192.168.1.30")
        {
            var ips = HostFinder.PingDevicesWithinRange(lowIp, uppIp);
            var list = HostFinder.FindDevicesWithPortOpen(ips); // HostFinder.ShowHostsInMyNetwork(ips);
            InfoLabel = "Done!";
            return list;
        }

        /// <summary>
        /// Loads saved hosts from .xml document. If there is no such document null is returned.
        /// </summary>
        /// <returns>Observable collection with loaded hosts or null if file was not found.</returns>
        private ObservableCollection<string>? LoadHosts()
        {
            if (!File.Exists("hosts.xml"))
            {
                return null;
            }

            try
            {
                InfoLabel = "Loading saved hosts...";
                XmlSerializer xmlSerializer = new(typeof(ObservableCollection<string>));
                using Stream stream = new FileStream("hosts.xml", FileMode.Open);

                var tempCollection = xmlSerializer.Deserialize(stream) as ObservableCollection<string>;
                InfoLabel = "Hosts loaded";
                return tempCollection;
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile("LoadHosts : " + e);
            }

            return null;
        }

        // ================================================================================
        // Events and Delegates 
        // ================================================================================

        private void OnChunkSent(object? sender, int progressBarMax)
        {
            _chunksToBeSent = progressBarMax;
            _chunksSent++;
        }

        private void OnEncryption(object? sender, EventArgs progressBarMax)
        {
            InfoLabel = "Encrypting file...";
        }
        
        private void OnDecryption(object? sender, EventArgs eventArgs)
        {
            InfoLabel = "Decrypting file...";
        }
        
        private void OnFileDownloaded(object? sender, string filename)
        {
            InfoLabel = "Received: " + filename;
            if (_directoryLock == false)
            {
                _directoryLock = true;
                OpenFolder();
            }
        }
        
        private void OnDownloadProgress(object? sender, int[] values)
        {
            try
            {
                var percent = values[0] * 100.0f / values[1]; 
                InfoLabel = percent.ToString("#.##") + "%";
                ProgressBarValue = percent;
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile(e.ToString());
            }
        }
        
    }
}