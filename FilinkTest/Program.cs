using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FilinkTest
{
    class Program
    {
        static void Main(string[] args)
        {
            
            Console.WriteLine("Hello World!");
        }
        
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
    }
}