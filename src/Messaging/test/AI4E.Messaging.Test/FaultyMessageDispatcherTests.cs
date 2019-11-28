using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public sealed class FaultyMessageDispatcherTests
    {
        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            return services.BuildServiceProvider();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddMessaging()
                .ConfigureMessageHandlers((messageHandlers, serviceProvider) =>
                {
                    messageHandlers.Register(
                        new MessageHandlerRegistration(typeof(string), _ => new FaultyMessageHandler()));

                    messageHandlers.Register(
                        new MessageHandlerRegistration(typeof(MemoryStream), _ => null));

                    messageHandlers.Register(
                       new MessageHandlerRegistration(typeof(IPAddress), _ => new FaultyMessageHandler()));

                    messageHandlers.Register(
                      new MessageHandlerRegistration(typeof(StringBuilder), _ => null));
                })
                .UseValidation();

            services.ConfigureApplicationParts(partManager =>
                partManager.ApplicationParts.Clear());

            services.AddSingleton<ITypeResolver, FaultyTypeResolver>();
        }

        private ServiceProvider ServiceProvider { get; set; }
        private IMessageDispatcher MessageDispatcher { get; set; }

        [TestInitialize]
        public void Setup()
        {
            ServiceProvider = ConfigureServices();
            MessageDispatcher = ServiceProvider.GetRequiredService<IMessageDispatcher>();
        }

        [TestCleanup]
        public void TearDown()
        {
            ServiceProvider.Dispose();
        }

        [TestMethod]
        public async Task FaultyMessageHanderLocalDispatchTest()
        {
            var dispatchResult = await MessageDispatcher.DispatchLocalAsync(
                DispatchDataDictionary.Create(string.Empty), publish: false);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(FaultyMessageHandlerException));
            Assert.AreEqual("FaultyMessageHandlerTest", exception.Message);
        }

        [TestMethod]
        public async Task NullMessageHanderLocalDispatchTest()
        {
            var dispatchResult = await MessageDispatcher.DispatchLocalAsync(
                DispatchDataDictionary.Create(new MemoryStream()), publish: false);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(InvalidOperationException));
        }

        [TestMethod]
        public async Task WrongTypeMessageHanderLocalDispatchTest()
        {
            var dispatchResult = await MessageDispatcher.DispatchLocalAsync(
                DispatchDataDictionary.Create(IPAddress.Loopback), publish: false);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(InvalidOperationException));
        }

        [TestMethod]
        public async Task FaultyMessageHanderDispatchTest()
        {
            var dispatchResult = await MessageDispatcher.DispatchAsync(
                DispatchDataDictionary.Create(string.Empty), publish: false);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(FaultyMessageHandlerException));
            Assert.AreEqual("FaultyMessageHandlerTest", exception.Message);
        }

        [TestMethod]
        public async Task NullMessageHanderDispatchTest()
        {
            var dispatchResult = await MessageDispatcher.DispatchAsync(
                DispatchDataDictionary.Create(new MemoryStream()), publish: false);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(InvalidOperationException));
        }

        [TestMethod]
        public async Task WrongTypeMessageHanderDispatchTest()
        {
            var dispatchResult = await MessageDispatcher.DispatchAsync(
                DispatchDataDictionary.Create(IPAddress.Loopback), publish: false);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(InvalidOperationException));
        }

        [TestMethod]
        public async Task FaultyTypeResolverDispatchTest()
        {
            var dispatchResult = await MessageDispatcher.DispatchAsync(
                DispatchDataDictionary.Create(new StringBuilder()), publish: false);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(FaultyTypeResolverException));
        }


        [TestMethod]
        public async Task FaultyMessageHanderDispatchToEndPointTest()
        {
            var localEndPoint = await MessageDispatcher.GetLocalEndPointAsync();
            var dispatchResult = await MessageDispatcher.DispatchAsync(
                DispatchDataDictionary.Create(string.Empty), publish: false, localEndPoint);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(FaultyMessageHandlerException));
            Assert.AreEqual("FaultyMessageHandlerTest", exception.Message);
        }

        [TestMethod]
        public async Task NullMessageHanderDispatchToEndPointTest()
        {
            var localEndPoint = await MessageDispatcher.GetLocalEndPointAsync();
            var dispatchResult = await MessageDispatcher.DispatchAsync(
                DispatchDataDictionary.Create(new MemoryStream()), publish: false, localEndPoint);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(InvalidOperationException));
        }

        [TestMethod]
        public async Task WrongTypeMessageHanderDispatchToEndPointTest()
        {
            var localEndPoint = await MessageDispatcher.GetLocalEndPointAsync();
            var dispatchResult = await MessageDispatcher.DispatchAsync(
                DispatchDataDictionary.Create(IPAddress.Loopback), publish: false, localEndPoint);

            var exception = (dispatchResult as FailureDispatchResult)?.Exception;

            Assert.IsFalse(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(exception, typeof(InvalidOperationException));
        }

        private sealed class FaultyMessageHandler : IMessageHandler
        {
            public Type MessageType => typeof(string);

            public ValueTask<IDispatchResult> HandleAsync(
                DispatchDataDictionary dispatchData,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                throw new FaultyMessageHandlerException();
            }
        }

        private sealed class FaultyMessageHandlerException : Exception
        {
            public FaultyMessageHandlerException() : base("FaultyMessageHandlerTest")
            { }
        }

        private sealed class FaultyTypeResolver : ITypeResolver
        {
            public bool TryResolveType(ReadOnlySpan<char> unqualifiedTypeName, [NotNullWhen(true)] out Type type)
            {
                if (unqualifiedTypeName.Equals(typeof(StringBuilder).GetUnqualifiedTypeName().AsSpan(), StringComparison.Ordinal))
                {
                    throw new FaultyTypeResolverException();
                }

                return TypeResolver.Default.TryResolveType(unqualifiedTypeName, out type);
            }
        }

        private sealed class FaultyTypeResolverException : Exception
        {
            public FaultyTypeResolverException() : base("FaultyTypeResolverTest")
            { }
        }
    }
}
