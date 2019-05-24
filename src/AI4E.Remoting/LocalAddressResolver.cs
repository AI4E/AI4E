/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Net;
using System.Net.Sockets;

namespace AI4E.Remoting
{
    public sealed class LocalAddressResolver : ILocalAddressResolver<IPAddress>
    {
        private readonly IPAddressFilter _ipAddressFilter;
        private readonly Func<IPAddress, bool> _predicate;

        public LocalAddressResolver(IPAddressFilter ipAddressFilter = default, Func<IPAddress, bool> predicate = null)
        {
            _ipAddressFilter = ipAddressFilter;
            _predicate = predicate;
        }

        public IPAddress GetLocalAddress()
        {
            IPAddress ipV4Address = null, ipV6Address = null;

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var address in host.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (_ipAddressFilter == IPAddressFilter.InterNetworkV6
                        || _predicate != null && !_predicate(address))
                    {
                        continue;
                    }

                    if (_ipAddressFilter == IPAddressFilter.Default)
                    {
                        return address;
                    }

                    ipV4Address = address;
                }
                else if (address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (_ipAddressFilter == IPAddressFilter.InterNetwork
                        || _predicate != null && !_predicate(address))
                    {
                        continue;
                    }

                    if (_ipAddressFilter == IPAddressFilter.Default)
                    {
                        return address;
                    }

                    ipV6Address = address;
                }
            }

            if (_ipAddressFilter == IPAddressFilter.PreferInterNetwork)
            {
                return ipV4Address ?? ipV6Address ?? IPAddress.Loopback;
            }

            if (_ipAddressFilter == IPAddressFilter.PreferInterNetworkV6)
            {
                return ipV6Address ?? ipV4Address ?? IPAddress.Loopback;
            }

            return IPAddress.Loopback;
        }
    }

    public enum IPAddressFilter
    {
        Default,
        InterNetwork,
        InterNetworkV6,
        PreferInterNetwork,
        PreferInterNetworkV6
    }
}
