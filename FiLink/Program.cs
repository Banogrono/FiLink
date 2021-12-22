using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Avalonia;
using Avalonia.ReactiveUI;
using FiLink.Models;

namespace FiLink
{
    static class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            var code = BasicProgramArgumentParser(args);
            if (code == -1 ) return;
            
            // This cancellation token allows me to break out of any loop form outside of thread and effectively end application run. 
            CancellationTokenSource cancellationTokenSource = new();
            var token = cancellationTokenSource.Token;


            if (SettingsAndConstants.EnableHostFinder)
            {
                // Running Identifier Service
                Identifier(token);
            }

            if (SettingsAndConstants.EnableServer)
            {
                // Running Server 
                Server(token);
            }

            if (!SettingsAndConstants.EnableConsoleMode)
            {
                // Running GUI thread
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            else
            {
                // Running CLI only
                ConsoleInterface cli = new ConsoleInterface();
                cli.RunCli();
            }

            // Killing the server and identifier
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        // This starts the listeners and then a server thread if client is pending.  
        private static void Server(CancellationToken token)
        {
            var info = new TcpListener(IPAddress.Any, 4398);
            var data = new TcpListener(IPAddress.Any, 4400);
            info.Start();
            data.Start();

            new Thread(() =>
            {
                int maxBackoff = 1000 * 8; // 8 seconds max
                int initialBackoff = 500;
                int backoff = initialBackoff;

                try
                {
                    while (true)
                    {
                        if (token.IsCancellationRequested) return;
                        Thread.Sleep(backoff);
                        backoff += 20;
                        if (backoff > maxBackoff) backoff = maxBackoff;
                        if (info.Pending())
                        {
                            var cInfo = info.AcceptTcpClient();
                            var cData = data.AcceptTcpClient();
                            Server s = new(ref cInfo, ref cData);
                            s.Receive();
                            backoff = initialBackoff;
                        }
                    }
                }
                catch (Exception e)
                {
                    UtilityMethods.LogToFile(e.ToString());
                }
            }).Start();
        }

        // This starts a new thread with name server that responds to request regarding hostname and availability of this host. 
        private static void Identifier(CancellationToken token)
        {
            var receiverThread = new Thread(() => HostFinder.HostnameResponder(token));
            receiverThread.Start();
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        private static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();

        // Basic command line arguments parser 
        private static int BasicProgramArgumentParser(string[] args)
        {
            if (args.Length == 0) return 0;

            var argsList = new List<string>(args);

            if (argsList.Contains("--cli"))
                SettingsAndConstants.EnableConsoleMode = true;

            if (argsList.Contains("--noserver") || argsList.Contains("-ns"))
                SettingsAndConstants.EnableServer = false;

            if (argsList.Contains("--nofinder") || argsList.Contains("-nf"))
                SettingsAndConstants.EnableHostFinder = false;

            if (argsList.Contains("--quiet") || argsList.Contains("-q"))
                SettingsAndConstants.EnableConsoleLog = false;

            if (argsList.Contains("--help") || argsList.Contains("-h"))
            {
                Console.WriteLine(SettingsAndConstants.Help);
                return -1;
            }
            
            if (argsList.Contains("--exit") || argsList.Contains("-e"))
            {
                return -1;
            }
            
            if (argsList.Contains("--faf"))
            {
                SettingsAndConstants.EnableConsoleMode = true;
                var cli = new ConsoleInterface();
                cli.FireAndForget(ref args);
                return -1;
            }

            return 0;
        }
    }
}