/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IPEndPointSerializer.cs 
 * Types:           AI4E.Remoting.IPEndPointSerializer
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Linq;
using System.Net;
using System.Text;

namespace AI4E.Remoting
{
    public sealed class IPEndPointSerializer : IAddressConversion<IPEndPoint>
    {
        public byte[] SerializeAddress(IPEndPoint route)
        {
            var s = route.ToString();

            return Encoding.UTF8.GetBytes(s);
        }

        public IPEndPoint DeserializeAddress(byte[] buffer)
        {
            var s = Encoding.UTF8.GetString(buffer);

            return Parse(s);
        }

        public string ToString(IPEndPoint route)
        {
            return route.ToString();
        }

        IPEndPoint IAddressConversion<IPEndPoint>.Parse(string str)
        {
            return Parse(str);
        }

        // https://stackoverflow.com/questions/2727609/best-way-to-create-ipendpoint-from-string
        private static IPEndPoint Parse(string endpointstring)
        {
            return Parse(endpointstring, -1);
        }

        private static IPEndPoint Parse(string endpointstring, int defaultport)
        {
            if (string.IsNullOrEmpty(endpointstring) || endpointstring.Trim().Length == 0)
            {
                throw new ArgumentException("Endpoint descriptor may not be empty.");
            }

            if (defaultport != -1 && (defaultport < IPEndPoint.MinPort || defaultport > IPEndPoint.MaxPort))
            {
                throw new ArgumentException(string.Format("Invalid default port '{0}'", defaultport));
            }

            var values = endpointstring.Split(new char[] { ':' });
            IPAddress ipaddy;
            var port = -1;

            //check if we have an IPv6 or ports
            if (values.Length <= 2) // ipv4 or hostname
            {
                if (values.Length == 1)
                    //no port is specified, default
                    port = defaultport;
                else
                    port = GetPort(values[1]);

                //try to use the address as IPv4, otherwise get hostname
                if (!IPAddress.TryParse(values[0], out ipaddy))
                    ipaddy = GetIPFromHost(values[0]);
            }
            else if (values.Length > 2) //ipv6
            {
                //could [a:b:c]:d
                if (values[0].StartsWith("[") && values[values.Length - 2].EndsWith("]"))
                {
                    var ipaddressstring = string.Join(":", values.Take(values.Length - 1).ToArray());
                    ipaddy = IPAddress.Parse(ipaddressstring);
                    port = GetPort(values[values.Length - 1]);
                }
                else //[a:b:c] or a:b:c
                {
                    ipaddy = IPAddress.Parse(endpointstring);
                    port = defaultport;
                }
            }
            else
            {
                throw new FormatException(string.Format("Invalid endpoint ipaddress '{0}'", endpointstring));
            }

            if (port == -1)
                throw new ArgumentException(string.Format("No port specified: '{0}'", endpointstring));

            return new IPEndPoint(ipaddy, port);
        }

        private static int GetPort(string p)
        {
            if (!int.TryParse(p, out var port) || port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new FormatException(string.Format("Invalid end point port '{0}'", p));
            }

            return port;
        }

        private static IPAddress GetIPFromHost(string p)
        {
            var hosts = Dns.GetHostAddresses(p);

            if (hosts == null || hosts.Length == 0)
                throw new ArgumentException(string.Format("Host not found: {0}", p));

            return hosts[0];
        }


    }
}
