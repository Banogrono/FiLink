using System;
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
            ConsoleInterface cli = new ConsoleInterface();
            var appMode = cli.ParseArguments(args);
            
            // This cancellation token allows me to break out of any loop form outside of thread and effectively end application run. 
            CancellationTokenSource cancellationTokenSource = new();
            var token = cancellationTokenSource.Token;

            if (appMode == 0)
            {
                cli.Dispose();
                
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

                // Running GUI thread
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }

            if (appMode == 1)
            {
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
            HostFinder.EnableConsoleLog = SettingsAndConstants.EnableConsoleLog;
            var receiverThread = new Thread(() => HostFinder.HostnameResponder(token));
            receiverThread.Start();
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        private static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}
