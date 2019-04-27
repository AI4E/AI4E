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
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Remoting
{
    public static class RemotingServiceCollectionExtension
    {
        public static void AddPhysicalEndPoint<TAddress, TPhysicalEndPoint>(this IServiceCollection services)
            where TPhysicalEndPoint : class, IPhysicalEndPoint<TAddress>

        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<IPhysicalEndPoint<TAddress>, TPhysicalEndPoint>();
            services.AddSingleton(typeof(IPhysicalEndPointMultiplexer<>), typeof(PhysicalEndPointMultiplexer<>));
            services.AddSingleton(new PhysicalEndPointMarkerService(typeof(TAddress)));
        }

        public static void AddUdpEndPoint(this IServiceCollection services)
        {
            services.AddPhysicalEndPoint<IPEndPoint, UdpEndPoint>();
            services.AddSingleton<IAddressConversion<IPEndPoint>, IPEndPointSerializer>();
        }

        public static void AddTcpEndPoint(this IServiceCollection services)
        {
            services.AddPhysicalEndPoint<IPEndPoint, TcpEndPoint>();
            services.AddSingleton<IAddressConversion<IPEndPoint>, IPEndPointSerializer>();
        }
    }

    public sealed class PhysicalEndPointMarkerService
    {
        public PhysicalEndPointMarkerService(Type addressType)
        {
            AddressType = addressType;
        }

        public Type AddressType { get; }
    }
}
