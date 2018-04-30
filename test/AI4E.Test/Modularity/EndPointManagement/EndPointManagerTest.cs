using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Routing;
using AI4E.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Modularity.EndPointManagement
{
    [TestClass]
    public class EndPointManagerTest
    {
        [TestMethod]
        public async Task DeliveryTest()
        {
            var buffer = new byte[] { 1, 2, 3 };

            var (x, y) = BuildMessagingSystem();

            var sourceEndPoint = EndPointRoute.CreateRoute("SourceEndPoint");
            var destinationEndPoint = EndPointRoute.CreateRoute("DestinationEndPoint");

            await x.AddEndPointAsync(sourceEndPoint, cancellation: default);
            await y.AddEndPointAsync(destinationEndPoint, cancellation: default);

            var message = new Message();

            using (var frameStream = message.PushFrame().OpenStream())
            {
                frameStream.Write(buffer, 0, buffer.Length);
            }

            var sendTask = x.SendAsync(message, destinationEndPoint, sourceEndPoint, cancellation: default);
            var receiveTask = y.ReceiveAsync(destinationEndPoint, cancellation: default);

            await Task.WhenAll(sendTask, receiveTask);

            var receivedMessage = await receiveTask;

            using (var frameStream = receivedMessage.PopFrame().OpenStream())
            {
                var receiveBuffer = new byte[4];

                Assert.AreEqual(3, frameStream.Read(receiveBuffer, 0, receiveBuffer.Length));
                Assert.AreEqual(1, receiveBuffer[0]);
                Assert.AreEqual(2, receiveBuffer[1]);
                Assert.AreEqual(3, receiveBuffer[2]);
            }
        }

        private IServiceProvider BuildServiceProvider(IPhysicalEndPoint<TestAddress> physicalEndPoint, IRouteMap<TestAddress> routeMap)
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IAddressConversion<TestAddress>, TestAddressSerializer>();
            services.AddSingleton<IEndPointScheduler<TestAddress>, RandomEndPointScheduler<TestAddress>>();
            services.AddEndPointManager<TestAddress>();
            services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();
            services.AddSingleton<IEndPointMultiplexer<TestAddress>>(p => new EndPointMultiplexer<TestAddress>(physicalEndPoint, p.GetService<ILogger<EndPointMultiplexer<TestAddress>>>()));
            //services.AddSingleton(physicalEndPoint);
            services.AddSingleton(routeMap);
            services.AddLogging(options =>
            {
                options.SetMinimumLevel(LogLevel.Trace);
                options.AddDebug();
            });
            return services.BuildServiceProvider();
        }

        private (IEndPointManager x, IEndPointManager y) BuildMessagingSystem()
        {
            var messagingSubsystem = new PhysicalMessingSubsystem();
            var routeMap = new RouteManager();
            var serviceProviderX = BuildServiceProvider(messagingSubsystem.X, routeMap);
            var serviceProviderY = BuildServiceProvider(messagingSubsystem.Y, routeMap);

            return (serviceProviderX.GetRequiredService<IEndPointManager>(), serviceProviderY.GetRequiredService<IEndPointManager>());
        }
    }

    public sealed class RouteManager : IRouteMap<TestAddress>
    {
        private readonly ConcurrentDictionary<EndPointRoute, RouteMap> _maps = new ConcurrentDictionary<EndPointRoute, RouteMap>();

        public Task<IEnumerable<TestAddress>> GetMapsAsync(EndPointRoute endPoint, CancellationToken cancellation)
        {
            return Task.FromResult(_maps.GetOrAdd(endPoint, _ => new RouteMap(endPoint)).Maps);
        }

        public Task<bool> MapRouteAsync(EndPointRoute localEndPoint, TestAddress address, DateTime leaseEnd, CancellationToken cancellation)
        {
            _maps.GetOrAdd(localEndPoint, _ => new RouteMap(localEndPoint)).Map(address, leaseEnd);

            return Task.FromResult(true);
        }

        public Task<bool> UnmapRouteAsync(EndPointRoute localEndPoint, TestAddress address, CancellationToken cancellation)
        {
            _maps.GetOrAdd(localEndPoint, _ => new RouteMap(localEndPoint)).Unmap(address);

            return Task.FromResult(true);
        }

        public Task UnmapRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            _maps.GetOrAdd(localEndPoint, _ => new RouteMap(localEndPoint)).Unmap();

            return Task.CompletedTask;
        }

        private sealed class RouteMap
        {
            private readonly EndPointRoute _route;
            private volatile ImmutableDictionary<TestAddress, DateTime> _maps = ImmutableDictionary<TestAddress, DateTime>.Empty;
            private readonly object _lock = new object();

            public RouteMap(EndPointRoute route)
            {
                if (route == null)
                    throw new ArgumentNullException(nameof(route));

                _route = route;
            }

            public IEnumerable<TestAddress> Maps => _maps.Where(p => p.Value > DateTime.Now).Select(p => p.Key);

            public void Unmap(TestAddress address)
            {
                lock (_lock)
                {
                    _maps = _maps.Remove(address);
                }
            }

            public void Unmap()
            {
                lock (_lock)
                {
                    _maps = ImmutableDictionary<TestAddress, DateTime>.Empty;
                }
            }

            public void Map(TestAddress address, DateTime leaseEnd)
            {
                lock (_lock)
                {
                    _maps = _maps.SetItem(address, leaseEnd);
                }
            }
        }
    }
}
