using System;
using System.Collections.Generic;
using System.Net;

namespace FiLink.Models
{
    
    // Did I wrote this? Where the hell did this class come from?
    
    /// <summary>
    /// Locator class is here to help you find active devices in your Local Area Network (LAN).
    /// The Locator class does 3 things:
    /// 1. Finds all active devices within given IP range
    /// 2. Checks whether those devices have designated ports open
    /// 3. Asks 'em for their name 
    /// </summary>
    public class Locator
    {

        public static List<string> FindActiveDevices(IPAddress lowerBound, IPAddress upperBound)
        {
            var activeDevices = new List<string>();


            var ipLow  = lowerBound.GetAddressBytes();
            var ipHigh = upperBound.GetAddressBytes();

            while (true)
            {
                // check ip here
                Console.WriteLine("Checking: " + new IPAddress(ipLow));
                    
                // increment ip, solution taken from: https://stackoverflow.com/questions/3483236/ip-address-increment-problem
                ipLow[3] = (byte)(ipLow[3] + 1);
                if (ipLow[3] == 0) {
                    ipLow[2] = (byte)(ipLow[2] + 1);
                    if (ipLow[2] == 0) {
                        ipLow[1] = (byte)(ipLow[1] + 1);
                        if (ipLow[1] == 0) {
                            ipLow[0] = (byte)(ipLow[0] + 1);
                        }
                    }
                }
                    
                // break when ip hits upper boundary
                if (ipLow == ipHigh) break;
            }

            return activeDevices;
        }

    }
}