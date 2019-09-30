using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class MessageDispatcherTests
    {
        [TestMethod]
        public async Task DispatchTest()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();
            var handler = new StringTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handlerRegistration = new MessageHandlerRegistration<string>(p => { handler.ServiceProvider = p; return handler; });
            registry.Register(handlerRegistration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<string>("testmessage");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.AreSame(desiredDispatchResult, dispatchResult);
            Assert.AreEqual(cancellation, handler.Cancellation);
            Assert.AreSame(dispatchData, handler.DispatchData);
            Assert.IsFalse(handler.Publish);
            Assert.IsTrue(handler.LocalDispatch);
            Assert.AreSame(serviceProvider, ((ServiceProviderMock)handler.ServiceProvider).Parent);
        }

        [TestMethod]
        public async Task P2PDispatchTest()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();

            var handler1 = new StringTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler1Registration = new MessageHandlerRegistration<string>(p => { handler1.ServiceProvider = p; return handler1; });

            var handler2 = new StringTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler2Registration = new MessageHandlerRegistration<string>(p => { handler2.ServiceProvider = p; return handler2; });

            registry.Register(handler1Registration);
            registry.Register(handler2Registration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<string>("testmessage");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.AreSame(desiredDispatchResult, dispatchResult);
            Assert.IsFalse(handler1.Called);
            Assert.IsTrue(handler2.Called);
        }

        [TestMethod]
        public async Task PublishDispatchTest()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();

            var handler1 = new StringTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler1Registration = new MessageHandlerRegistration<string>(p => { handler1.ServiceProvider = p; return handler1; });

            var handler2 = new StringTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler2Registration = new MessageHandlerRegistration<string>(p => { handler2.ServiceProvider = p; return handler2; });

            registry.Register(handler1Registration);
            registry.Register(handler2Registration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<string>("testmessage");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: true, cancellation);

            Assert.IsInstanceOfType(dispatchResult, typeof(IAggregateDispatchResult));
            Assert.IsTrue(((IAggregateDispatchResult)dispatchResult).DispatchResults.SequenceEqual(new[] { desiredDispatchResult, desiredDispatchResult }));
            Assert.IsTrue(handler1.Called);
            Assert.IsTrue(handler1.Publish);
            Assert.IsTrue(handler2.Called);
            Assert.IsTrue(handler2.Publish);
        }

        [TestMethod]
        public async Task P2PRouteDescendTest()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();

            var handler1 = new BaseMessageTypeTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler1Registration = new MessageHandlerRegistration<BaseMessageType>(p => handler1);

            var handler2 = new DerivedMessageTypeTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler2Registration = new MessageHandlerRegistration<DerivedMessageType>(p => handler2);

            registry.Register(handler1Registration);
            registry.Register(handler2Registration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<DerivedMessageType>(new DerivedMessageType());
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.IsFalse(handler1.Called);
            Assert.IsTrue(handler2.Called);
        }

        [TestMethod]
        public async Task P2PRouteDescend2Test()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();

            var handler1 = new BaseMessageTypeTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler1Registration = new MessageHandlerRegistration<BaseMessageType>(p => handler1);

            registry.Register(handler1Registration);

            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<DerivedMessageType>(new DerivedMessageType());
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.IsTrue(handler1.Called);
        }

        [TestMethod]
        public async Task PublishRouteDescendTest()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();

            var handler1 = new BaseMessageTypeTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler1Registration = new MessageHandlerRegistration<BaseMessageType>(p => handler1);

            var handler2 = new DerivedMessageTypeTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler2Registration = new MessageHandlerRegistration<DerivedMessageType>(p => handler2);

            registry.Register(handler1Registration);
            registry.Register(handler2Registration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<DerivedMessageType>(new DerivedMessageType());
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: true, cancellation);

            Assert.IsTrue(handler1.Called);
            Assert.IsTrue(handler2.Called);
        }

        [TestMethod]
        public async Task PublishRouteDescend2Test()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();

            var handler1 = new BaseMessageTypeTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler1Registration = new MessageHandlerRegistration<BaseMessageType>(p => handler1);

            registry.Register(handler1Registration);

            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<DerivedMessageType>(new DerivedMessageType());
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: true, cancellation);

            Assert.IsTrue(handler1.Called);
        }

        [TestMethod]
        public async Task P2PNoRoutesTest()
        {
            var registry = new MessageHandlerRegistry();
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<DerivedMessageType>(new DerivedMessageType());
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.IsInstanceOfType(dispatchResult, typeof(DispatchFailureDispatchResult));
        }

        [TestMethod]
        public async Task PublishNoRoutesTest()
        {
            var registry = new MessageHandlerRegistry();
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<DerivedMessageType>(new DerivedMessageType());
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: true, cancellation);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
        }

        [TestMethod]
        public async Task P2PHandlerDispatchFailureTest()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();

            var handler1 = new StringTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler1Registration = new MessageHandlerRegistration<string>(p => { handler1.ServiceProvider = p; return handler1; });

            var handler2 = new StringTestMessageHandler { DispatchResult = new DispatchFailureDispatchResult(typeof(string)) };
            var handler2Registration = new MessageHandlerRegistration<string>(p => { handler2.ServiceProvider = p; return handler2; });

            registry.Register(handler1Registration);
            registry.Register(handler2Registration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<string>("testmessage");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.AreSame(desiredDispatchResult, dispatchResult);
            Assert.IsTrue(handler1.Called);
            Assert.IsTrue(handler2.Called);
        }

        [TestMethod]
        public async Task P2PPublishOnlyTest()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();

            var handler1 = new BaseMessageTypeTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler1Registration = new MessageHandlerRegistration<BaseMessageType>(p => handler1);

            var handler2 = new DerivedMessageTypeTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handler2Config = new MessageHandlerConfigurationBuilder()
                .Configure(() => new PublishOnlyMessageHandlerConfiguration(true))
                .Build();

            var handler2Registration = new MessageHandlerRegistration<DerivedMessageType>(handler2Config, p => handler2);

            registry.Register(handler1Registration);
            registry.Register(handler2Registration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<DerivedMessageType>(new DerivedMessageType());
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.IsTrue(handler1.Called);
            Assert.IsFalse(handler2.Called);
        }

        [TestMethod]
        public async Task WrapExceptionTest()
        {
            var registry = new MessageHandlerRegistry();
            var handler = new ThrowExcentionTestMessageHandler();
            var handlerRegistration = new MessageHandlerRegistration<string>(p => handler);
            registry.Register(handlerRegistration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<string>("testmessage");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.IsInstanceOfType(dispatchResult, typeof(FailureDispatchResult));
        }

        [TestMethod]
        public async Task WrongTypeHandlerTest()
        {
            var registry = new MessageHandlerRegistry();
            var handler = new WrongTypeTestMessageHandler();
            var handlerRegistration = new MessageHandlerRegistration<string>(p => handler);
            registry.Register(handlerRegistration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<string>("testmessage");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);
            });
        }

        [TestMethod]
        public async Task NullHandlerTest()
        {
            var registry = new MessageHandlerRegistry();
            var handler = new WrongTypeTestMessageHandler();
            var handlerRegistration = new MessageHandlerRegistration<string>(p => null);
            registry.Register(handlerRegistration);
            var serviceProvider = new ServiceProviderMock();
            var dispatcher = new MessageDispatcher(registry, serviceProvider);

            var dispatchData = new DispatchDataDictionary<string>("testmessage");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);
            });
        }
    }

    public class BaseMessageType { }

    public class DerivedMessageType : BaseMessageType { }

    public class StringTestMessageHandler : IMessageHandler<string>
    {
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<string> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            DispatchData = dispatchData;
            Publish = publish;
            LocalDispatch = localDispatch;
            Cancellation = cancellation;
            Called = true;

            return new ValueTask<IDispatchResult>(DispatchResult);
        }

        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            DispatchData = dispatchData;
            Publish = publish;
            LocalDispatch = localDispatch;
            Cancellation = cancellation;
            Called = true;

            return new ValueTask<IDispatchResult>(DispatchResult);
        }

        public Type MessageType => typeof(string);

        public IDispatchResult DispatchResult { get; set; }
        public IServiceProvider ServiceProvider { get; set; }

        public DispatchDataDictionary DispatchData { get; private set; }
        public bool Publish { get; private set; }
        public bool LocalDispatch { get; private set; }
        public CancellationToken Cancellation { get; private set; }
        public bool Called { get; private set; }

    }

    public class BaseMessageTypeTestMessageHandler : IMessageHandler<BaseMessageType>
    {
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<BaseMessageType> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Called = true;

            return new ValueTask<IDispatchResult>(DispatchResult);
        }

        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Called = true;

            return new ValueTask<IDispatchResult>(DispatchResult);
        }

        public Type MessageType => typeof(BaseMessageType);

        public IDispatchResult DispatchResult { get; set; }
        public bool Called { get; private set; }
    }

    public class DerivedMessageTypeTestMessageHandler : IMessageHandler<DerivedMessageType>
    {
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<DerivedMessageType> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Called = true;

            return new ValueTask<IDispatchResult>(DispatchResult);
        }

        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Called = true;

            return new ValueTask<IDispatchResult>(DispatchResult);
        }

        public Type MessageType => typeof(DerivedMessageType);

        public IDispatchResult DispatchResult { get; set; }
        public bool Called { get; private set; }
    }

    public class ThrowExcentionTestMessageHandler : IMessageHandler<string>
    {
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<string> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            throw new Exception();
        }

        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            throw new Exception();
        }

        public Type MessageType => typeof(string);
    }

    public class WrongTypeTestMessageHandler : IMessageHandler<string>
    {
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<string> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            throw null;
        }

        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            throw null;
        }

        public Type MessageType => typeof(BaseMessageType);
    }
}
