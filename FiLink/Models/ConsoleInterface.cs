using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FiLink.ViewModels;

namespace FiLink.Models
{
    public class ConsoleInterface : IDisposable
    {
        private static readonly string _help = "Welcome to FiLink File Transfer Application!\n" +
                                               "Available arguments: \n" +
                                               "--help - opens this help.\n" +
                                               "--cli - launches application without GUI in text interface mode.\n" +
                                               "--faf <ip> <path_to_file> - (fire and forget) sends a file and closes application.\n" +
                                               "--quiet - disables most console logging.\n" +
                                               "--noserver - disables server. Warning: application cannot receive files in this mode!\n" +
                                               "--nofinder - disables Host Finder. Warning: hosts can not be searched for/ found in this mode!\n" +
                                               "--ips <ip>... - add IP addresses of hosts, each address has to be separated by space.\n" +
                                               "--files <path_to_file>... - add files, each path to file has to be separated with space. Names containing spaces have to be inside \"\" \n" +
                                               "--send - sends all entered files to all entered hosts.\n" +
                                               "--clearFiles - clears files list.\n" +
                                               "--clearHost - clears host list.\n" +
                                               "--exit - closes the application.\n";

        private MainWindowViewModel? _viewModel = new();

        private void SendFiles()
        {
            if (_viewModel == null)
            {
                return;
            }

            try
            {
                Console.WriteLine();
                Task.Run(() => _viewModel.SendFiles());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                UtilityMethods.LogToFile(e.ToString());
            }
        }

        private void ClearFiles(string file = "")
        {
            if (_viewModel == null)
            {
                return;
            }

            try
            {
                if (file == "")
                {
                    _viewModel.SelectedFiles.Clear();
                }
                else
                {
                    if (_viewModel.SelectedFiles.Contains(file))
                    {
                        _viewModel.SelectedFiles.Remove(file);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                UtilityMethods.LogToFile(e.ToString());
            }
        }

        private void ClearHosts(string host = "")
        {
            if (_viewModel == null)
            {
                return;
            }

            try
            {
                if (host == "")
                {
                    _viewModel.SelectedHosts.Clear();
                }
                else
                {
                    if (_viewModel.SelectedHosts.Contains(host))
                    {
                        _viewModel.SelectedHosts.Remove(host);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                UtilityMethods.LogToFile(e.ToString());
            }
        }

        private void GetIps(List<string> args)
        {
            if (_viewModel == null)
            {
                return;
            }

            var index = args.IndexOf("--ips");
            for (int i = index + 1; i < args.Count; i++)
            {
                var entry = args[i];
                if (entry.Contains("--"))
                {
                    break;
                }

                if (IPAddress.TryParse(entry, out _))
                {
                    _viewModel.SelectedHosts.Add(entry);
                }
            }
        }

        private void GetFiles(List<string> args)
        {
            if (_viewModel == null)
            {
                return;
            }

            var index = args.IndexOf("--files");
            for (int i = index + 1; i < args.Count; i++)
            {
                var entry = args[i];
                // if (entry.Contains("--")) // technically speaking file name can contain '--'...
                // {
                //     break;
                // }
                if (File.Exists(entry))
                {
                    _viewModel.SelectedFiles.Add(entry);
                }
            }
        }

        // todo: add args parser
        public int ParseArguments(string[]? args) // todo: add default answer
        {
            if (_viewModel == null)
            {
                return -3;
            }

            if (args == null)
            {
                return -2;
            }

            if (args.Length > 0)
            {
                var argsList = new List<string>(args); // todo: upgrade this to a switch 

                if (argsList.Contains("--help"))
                {
                    Console.WriteLine(_help);
                }

                if (argsList.Contains("--ips"))
                {
                    GetIps(argsList);
                }

                if (argsList.Contains("--files"))
                {
                    GetFiles(argsList);
                }

                if (argsList.Contains("--send"))
                {
                    if (_viewModel.SelectedFiles.Count == 0 || _viewModel.SelectedHosts.Count == 0)
                    {
                        Console.WriteLine("No selected files/ hosts.");
                        return 0;
                    }

                    SendFiles();
                }

                if (argsList.Contains("--noserver"))
                {
                    SettingsAndConstants.EnableServer = !SettingsAndConstants.EnableServer;
                    Console.WriteLine($"Server {(SettingsAndConstants.EnableServer ? "enabled" : "disabled")}.");
                }

                if (argsList.Contains("--nofinder"))
                {
                    SettingsAndConstants.EnableHostFinder = !SettingsAndConstants.EnableHostFinder;
                    Console.WriteLine(
                        $"Host Finder {(SettingsAndConstants.EnableHostFinder ? "enabled" : "disabled")}.");
                }

                if (argsList.Contains("--quiet"))
                {
                    SettingsAndConstants.EnableConsoleLog = !SettingsAndConstants.EnableConsoleLog;
                }

                if (argsList.Contains("--cli"))
                {
                    if (SettingsAndConstants.EnableConsoleMode)
                    {
                        Console.WriteLine("Application already runs in console mode.");
                        return 0;
                    }

                    Console.WriteLine("Running in Command Line Mode.");
                    SettingsAndConstants.EnableConsoleMode = true;
                    return 1;
                }

                if (argsList.Contains("--exit"))
                {
                    Console.WriteLine("Bye.");
                    return -1;
                }

                if (argsList.Contains("--faf"))
                {
                    var index = argsList.IndexOf("--faf");
                    var ip = args[index + 1];
                    var filePath = args[index + 2];

                    if (!IPAddress.TryParse(ip, out _) || !File.Exists(filePath))
                    {
                        Console.WriteLine("IP address and/ or file path is not correct.");
                    }

                    ClearFiles();
                    ClearHosts();
                    _viewModel.SelectedFiles.Add(filePath);
                    _viewModel.SelectedHosts.Add(ip);
                    SendFiles();
                    Thread.Sleep(1000); //todo: add some kind of await
                    return -1;
                }
            }

            return 0;
        }

        public void RunCli()
        {
            Console.WriteLine("Welcome to FiLink File Transfer Application!");
            Console.WriteLine("Please enter a command or enter --help to get help.");
            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine()?.Split(" ");
                var state = ParseArguments(input);

                if (state == -1)
                {
                    return;
                }
            }
        }


        public void Dispose()
        {
            _viewModel = null;
        }
    }
}