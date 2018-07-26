using System;
using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Remoting
{
    public static class ServiceCollectionExtension
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
