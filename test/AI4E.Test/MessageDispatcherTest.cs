using System;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test
{
    [TestClass]
    public class MessageDispatcherTest
    {
        [TestMethod]
        public void ConstructionTest()
        {
            var serviceProvider = BuildServiceProvider();
            var messageDispatcher = new MessageDispatcher(serviceProvider);
        }

        [TestMethod]
        public async Task BaseTest()
        {
            var serviceProvider = BuildServiceProvider();
            var messageDispatcher = new MessageDispatcher(serviceProvider);

            // Register a handler for the message type

            var handler = new TestMessageHandler();
            var handlerProvider = ContextualProvider.Create(handler);
            messageDispatcher.Register(handlerProvider);

            var handler2 = new TestMessageHandler();
            messageDispatcher.Register(ContextualProvider.Create(handler2));

            var baseHandler = new TestMessageBaseHandler();
            messageDispatcher.Register(ContextualProvider.Create(baseHandler));

            // If the handler (provider) is registered again, the order of handlers shall stay the same.
            messageDispatcher.Register(handlerProvider);

            var message = new TestMessage("x", 123);

            var dispatchResult = await messageDispatcher.DispatchAsync(message, new DispatchValueDictionary(), publish: false, cancellation: default);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.IsNull(handler.Message); // Handler shall not be called
            Assert.AreEqual(message, handler2.Message); // Handler2 shall be called
            Assert.IsNull(baseHandler.Message); // BaseHandler shall not be called
        }


        [TestMethod]
        public async Task NoHandlerTest()
        {
            var serviceProvider = BuildServiceProvider();
            var messageDispatcher = new MessageDispatcher(serviceProvider);

            // Register no handler for the message type

            var message = new TestMessage("x", 123);

            var dispatchResult = await messageDispatcher.DispatchAsync(message, new DispatchValueDictionary(), publish: false, cancellation: default);

            Assert.IsFalse(dispatchResult.IsSuccess);
            // TODO: Shall we specify the exact result type? Introduce interfaces for the different dispatch results?
            Assert.IsInstanceOfType(dispatchResult, typeof(DispatchFailureDispatchResult));
        }

        [TestMethod]
        public async Task NoHandlerPublishTest()
        {
            var serviceProvider = BuildServiceProvider();
            var messageDispatcher = new MessageDispatcher(serviceProvider);

            // Register no handler for the message type

            var message = new TestMessage("x", 123);

            var dispatchResult = await messageDispatcher.DispatchAsync(message, new DispatchValueDictionary(), publish: true, cancellation: default);

            Assert.IsTrue(dispatchResult.IsSuccess);
        }

        [TestMethod]
        public async Task DeregisterTest()
        {
            var serviceProvider = BuildServiceProvider();
            var messageDispatcher = new MessageDispatcher(serviceProvider);

            // Register a handler for the message type and deregister it to create a typed dispatcher
            var handler = new TestMessageHandler();
            var registration = messageDispatcher.Register(ContextualProvider.Create(handler));

            var baseHandler = new TestMessageBaseHandler();
            messageDispatcher.Register(ContextualProvider.Create(baseHandler));

            registration.Cancel();
            await registration.Cancellation;

            var message = new TestMessage("x", 123);

            var dispatchResult = await messageDispatcher.DispatchAsync(message, new DispatchValueDictionary(), publish: false, cancellation: default);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.IsNull(handler.Message);
            Assert.AreEqual(message, baseHandler.Message);
        }

        [TestMethod]
        public async Task PublishTest()
        {
            var serviceProvider = BuildServiceProvider();
            var messageDispatcher = new MessageDispatcher(serviceProvider);

            // Register a handler for the message type

            var handler = new TestMessageHandler();
            messageDispatcher.Register(ContextualProvider.Create(handler));

            var handler2 = new TestMessageHandler();
            messageDispatcher.Register(ContextualProvider.Create(handler2));

            var baseHandler = new TestMessageBaseHandler();
            messageDispatcher.Register(ContextualProvider.Create(baseHandler));

            var message = new TestMessage("x", 123);

            var dispatchResult = await messageDispatcher.DispatchAsync(message, new DispatchValueDictionary(), publish: true, cancellation: default);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(message, handler.Message);
            Assert.AreEqual(message, handler2.Message);
            Assert.AreEqual(message, baseHandler.Message);
        }

        // If no handler is available for the message type, it shall be dispatched to a handler of its base type.
        [TestMethod]
        public async Task DispatchToBaseHandlerTest()
        {
            var serviceProvider = BuildServiceProvider();
            var messageDispatcher = new MessageDispatcher(serviceProvider);

            // Register a handler for the message type

            var handler = new TestMessageBaseHandler();
            messageDispatcher.Register(ContextualProvider.Create(handler));

            var message = new TestMessage("x", 123);

            var dispatchResult = await messageDispatcher.DispatchAsync(message, new DispatchValueDictionary(), publish: false, cancellation: default);

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.AreEqual(message, handler.Message);
        }

        public IServiceProvider BuildServiceProvider()
        {
            IServiceCollection services = new ServiceCollection();

            return services.BuildServiceProvider();
        }
    }

    public class TestMessageHandler : IMessageHandler<TestMessage>
    {
        public TestMessage Message = null;

        public Task<IDispatchResult> HandleAsync(TestMessage message, DispatchValueDictionary context)
        {
            Message = message;

            return Task.FromResult<IDispatchResult>(new SuccessDispatchResult());
        }
    }

    public class TestMessageBaseHandler : IMessageHandler<TestMessageBase>
    {
        public TestMessageBase Message = null;

        public Task<IDispatchResult> HandleAsync(TestMessageBase message, DispatchValueDictionary context)
        {
            Message = message;

            return Task.FromResult<IDispatchResult>(new SuccessDispatchResult());
        }
    }

    public class TestMessageBase
    {
        public TestMessageBase(string x)
        {
            X = x;
        }

        public string X { get; }
    }

    public class TestMessage : TestMessageBase
    {
        public TestMessage(string x, float y) : base(x)
        {
            Y = y;
        }

        public float Y { get; }
    }
}
