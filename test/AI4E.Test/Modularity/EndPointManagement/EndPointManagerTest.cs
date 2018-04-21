using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;

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
            services.AddSingleton(physicalEndPoint);
            services.AddSingleton(routeMap);
            services.AddLogging(options =>
            {
                options.AddConsole();
                options.SetMinimumLevel(LogLevel.Trace);
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

    public enum TestAddress : byte
    {
        X = 0,
        Y = 1
    }

    public sealed class TestAddressSerializer : IAddressConversion<TestAddress>
    {
        public byte[] SerializeAddress(TestAddress route)
        {
            return new[] { (byte)route };
        }

        public TestAddress DeserializeAddress(byte[] buffer)
        {
            return (TestAddress)buffer[0];
        }

        public string ToString(TestAddress route)
        {
            switch (route)
            {
                case TestAddress.X:
                    return "X";
                case TestAddress.Y:
                    return "Y";
                default:
                    throw new ArgumentException();
            }
        }

        public TestAddress Parse(string str)
        {
            switch (str)
            {
                case "X":
                    return TestAddress.X;
                case "Y":
                    return TestAddress.Y;
                default:
                    throw new ArgumentException();
            }
        }
    }

    public sealed class PhysicalMessingSubsystem
    {
        private readonly AsyncProducerConsumerQueue<IMessage> _xToY = new AsyncProducerConsumerQueue<IMessage>();
        private readonly AsyncProducerConsumerQueue<IMessage> _yToX = new AsyncProducerConsumerQueue<IMessage>();

        public PhysicalMessingSubsystem()
        {
            X = new PhysicalEndPoint(TestAddress.X, _xToY, _yToX);
            Y = new PhysicalEndPoint(TestAddress.Y, _yToX, _xToY);
        }

        public IPhysicalEndPoint<TestAddress> X { get; }

        public IPhysicalEndPoint<TestAddress> Y { get; }

        private sealed class PhysicalEndPoint : IPhysicalEndPoint<TestAddress>
        {
            private readonly AsyncProducerConsumerQueue<IMessage> _txQueue;
            private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue;

            public PhysicalEndPoint(TestAddress localAddress,
                                    AsyncProducerConsumerQueue<IMessage> txQueue,
                                    AsyncProducerConsumerQueue<IMessage> rxQueue)
            {
                if (txQueue == null)
                    throw new ArgumentNullException(nameof(txQueue));

                if (rxQueue == null)
                    throw new ArgumentNullException(nameof(rxQueue));

                LocalAddress = localAddress;
                _txQueue = txQueue;
                _rxQueue = rxQueue;
            }

            public Task SendAsync(IMessage message, TestAddress address, CancellationToken cancellation)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (address < TestAddress.X || address > TestAddress.Y)
                    throw new ArgumentException("address");

                if (address == LocalAddress)
                {
                    return _rxQueue.EnqueueAsync(message, cancellation);
                }
                else
                {
                    return _txQueue.EnqueueAsync(message, cancellation);
                }
            }

            public Task<IMessage> ReceiveAsync(CancellationToken cancellation)
            {
                return _rxQueue.DequeueAsync(cancellation);
            }

            public TestAddress LocalAddress { get; }
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
