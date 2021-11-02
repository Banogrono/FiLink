using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace FiLink.Models
{
    public static class HostFinder
    {
        // =============================================================================================================
        // Public Methods
        // =============================================================================================================

        /// <summary>
        /// Pings devices in given range.
        /// </summary>
        /// <param name="lowerIpBoundary">Pinging starts from this address (inclusive).</param>
        /// <param name="upperIpBoundary">Pinging ends on this address (exclusive).</param>
        /// <returns>List of devices that responded to ping.</returns>
        public static List<string> PingDevicesWithinRange
            (string lowerIpBoundary = "192.168.1.2", string upperIpBoundary = "192.168.1.255")
        {
            int lower;
            int upper;
            string rest;
            try
            {
                lower = int.Parse(lowerIpBoundary.Split(".")[3]);
                upper = int.Parse(upperIpBoundary.Split(".")[3]);
                var arr = lowerIpBoundary.Split(".");
                rest = arr[0] + "." + arr[1] + "." + arr[2] + ".";
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null!;
            }

            var ipList = new List<string>();

            Ping ping = new();
            for (int i = lower; i < upper; i++)
            {
                var ip = rest + i;
                Console.Write($"Pinging: {ip} ");
                var reply = ping.Send(ip, SettingsAndConstants.PingTimeout);
                if (reply is { Status: IPStatus.Success })
                {
                    Console.WriteLine("- Success.");

                    ipList.Add(ip);
                }
                else
                {
                    Console.WriteLine("- Fail.");
                }

                // event for progress bar 
                OnHostSearchProgressed?.Invoke(null, null!);
            }

            return ipList;
        }


        /// NAME CLIENT =================================================================================================
        /// <summary>
        /// Name client iterates over a given list of IPs and sends request for host name to each IP. If response is
        /// received, we can assume that given host is valid.
        /// </summary>
        /// <param name="ips">List of IPs to iterate over</param>
        /// <returns>List of host names and ips merged together: [hostname]:[ip]</returns>
        public static List<string> ShowHostsInMyNetwork(List<string> ips)
        {
            Console.WriteLine("Communicating with devices...");
            var hostList = new List<string>();

            foreach (var ip in ips)
            {
                var client = new TcpClient();
                try
                {
                    var result = client.BeginConnect(ip, 4404, null, null);

                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                    if (!success)
                    {
                        throw new Exception("Failed to connect.");
                    }

                    var stream = client.GetStream();

                    var command = Encoding.UTF8.GetBytes("get_host");
                    stream.Write(command, 0, command.Length);
                    stream.Flush();

                    var buffer = new byte[1024];
                    while (true)
                    {
                        //Task.Delay(50);
                        if (client.Available > 0)
                        {
                            stream.Read(buffer, 0, buffer.Length);
                            break;
                        }
                    }

                    var hostInfo = Encoding.UTF8.GetString(buffer).Trim();
                    if (hostInfo.Contains(":"))
                    {
                        var hostNameIp = UtilityMethods.RemoveNulls(hostInfo.Split(":")[1]) + ":" + ip;
                        hostList.Add(hostNameIp);
                    }

                    client.EndConnect(result);
                }
                catch (Exception e)
                {
                    UtilityMethods.LogToFile("ShowHostsInMyNetwork : " + e);
                }
                finally
                {
                    client.Close();
                }
            }

            return hostList;
        }
        
        public static List<string> FindDevicesWithPortOpen(List<string> ips, int port = 4404, int timeout = 100)
        {
            List<string> validAddresses = new List<string>();
            foreach (var ip in ips)
            {
                Console.WriteLine("Checking " + ip);

                try
                {
                    var client = new TcpClient();

                    if (client.ConnectAsync(ip, port).Wait(timeout))
                    {
                        validAddresses.Add(ip);
                    }
                    client.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            return validAddresses;
        } 

        /// NAME SERVER/ RECEIVER =======================================================================================
        /// <summary>
        /// Receives request for hostname and sends back current machine hostname.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token needed for breaking receiving loop.</param>
        public static void HostnameResponder(CancellationToken cancellationToken)
        {
            TcpListener tcpListener = new(IPAddress.Any, 4404);
            tcpListener.Start();

            var maxBackoff = 1000 * 1; // 1 second max
            var initialBackoff = 20;
            var backoff = initialBackoff;
            try
            {
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (tcpListener.Pending())
                    {
                        Console.WriteLine("Client connected.");
                        var client = tcpListener.AcceptTcpClient();
                        var stream = client.GetStream();
                        var buffer = new byte[128]; // experimental -> does not support seek

                        while (true)
                        {
                            if (client.Available > 0)
                            {
                                stream.Read(buffer, 0, buffer.Length);
                                break;
                            }
                            if (cancellationToken.IsCancellationRequested)
                            {
                                stream.Close();
                                client.Close();
                                throw new Exception("Issued cancellation token while in connection with client.");
                            }
                        }

                        var command = Encoding.UTF8.GetString(buffer);
                        if (!command.Contains("get_host"))
                        {
                            continue;
                        }

                        var response = "hostname:" + Environment.MachineName;
                        response = response.Trim();
                        var responseEncoded = Encoding.UTF8.GetBytes(response);

                        stream.Write(responseEncoded, 0, responseEncoded.Length);
                        stream.Flush();

                        stream.Close();
                        client.Close();
                        backoff = initialBackoff;
                    }

                    Thread.Sleep(backoff);
                    backoff += 20;
                    if (backoff > maxBackoff)
                    {
                        backoff = maxBackoff;
                    }
                }
            }
            catch (Exception e)
            {
                UtilityMethods.LogToFile("HostnameResponder : " + e);
            }

            tcpListener.Stop();
            Console.WriteLine("Receiver terminated.");
        }

        // =============================================================================================================
        // Events
        // =============================================================================================================

        /// <summary>
        /// Event handler that is invoked on host-searching methods progress. 
        /// </summary>
        public static event EventHandler? OnHostSearchProgressed;
    }
}