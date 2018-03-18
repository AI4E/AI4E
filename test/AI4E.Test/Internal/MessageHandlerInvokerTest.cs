using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Internal
{
    [TestClass]
    public sealed class MessageHandlerInvokerTest
    {
        private readonly List<MessageHandlerActionDescriptor> _descriptors = new List<MessageHandlerActionDescriptor>();

        public MessageHandlerInvokerTest()
        {
            foreach (var method in typeof(MessageHandler).GetMethods().Where(p => p.Name.Contains("Handle")))
            {
                _descriptors.Add(new MessageHandlerActionDescriptor(method.GetParameters().First().ParameterType, method));
            }
        }

        [TestMethod]
        public async Task WithDispatchResultTest()
        {
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var messageHandler = new MessageHandler();

            var method = typeof(MessageHandler).GetMethod("Handle", new[] { typeof(MessageX) });
            var descriptor = new MessageHandlerActionDescriptor(typeof(MessageX), method);

            var invoker = new MessageHandlerInvoker<MessageX>(messageHandler, descriptor, ImmutableArray<IContextualProvider<IMessageProcessor>>.Empty, serviceProvider);

            var dispatchResult = await invoker.HandleAsync(new MessageX(), new DispatchValueDictionary());

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult<int>));
            Assert.AreEqual(42, ((SuccessDispatchResult<int>)dispatchResult).Result);
        }

        [TestMethod]
        public async Task WithVoidResultTest()
        {
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var messageHandler = new MessageHandler();

            var method = typeof(MessageHandler).GetMethod("Handle", new[] { typeof(MessageY) });
            var descriptor = new MessageHandlerActionDescriptor(typeof(MessageX), method);

            var invoker = new MessageHandlerInvoker<MessageY>(messageHandler, descriptor, ImmutableArray<IContextualProvider<IMessageProcessor>>.Empty, serviceProvider);

            var dispatchResult = await invoker.HandleAsync(new MessageY { I = 34 }, new DispatchValueDictionary());

            Assert.IsTrue(dispatchResult.IsSuccess);

            dispatchResult = await invoker.HandleAsync(new MessageY { I = 34, Fail = true }, new DispatchValueDictionary());

            Assert.IsFalse(dispatchResult.IsSuccess);
        }

        [TestMethod]
        public async Task WithDispatchResultTaskTest()
        {
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var messageHandler = new MessageHandler();

            var method = typeof(MessageHandler).GetMethod("HandleAsync", new[] { typeof(MessageX) });
            var descriptor = new MessageHandlerActionDescriptor(typeof(MessageX), method);

            var invoker = new MessageHandlerInvoker<MessageX>(messageHandler, descriptor, ImmutableArray<IContextualProvider<IMessageProcessor>>.Empty, serviceProvider);

            var dispatchResult = await invoker.HandleAsync(new MessageX(), new DispatchValueDictionary());

            Assert.IsTrue(dispatchResult.IsSuccess);
            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult<int>));
            Assert.AreEqual(42, ((SuccessDispatchResult<int>)dispatchResult).Result);
        }

        [TestMethod]
        public async Task WithTaskResultTest()
        {
            var services = new ServiceCollection();
            var serviceProvider = services.BuildServiceProvider();
            var messageHandler = new MessageHandler();

            var method = typeof(MessageHandler).GetMethod("HandleAsync", new[] { typeof(MessageY) });
            var descriptor = new MessageHandlerActionDescriptor(typeof(MessageX), method);

            var invoker = new MessageHandlerInvoker<MessageY>(messageHandler, descriptor, ImmutableArray<IContextualProvider<IMessageProcessor>>.Empty, serviceProvider);

            var dispatchResult = await invoker.HandleAsync(new MessageY { I = 34 }, new DispatchValueDictionary());

            Assert.IsTrue(dispatchResult.IsSuccess);
        }
    }

    public class MessageHandler
    {
        public IDispatchResult Handle(MessageX message)
        {
            Assert.IsNotNull(message);

            return new SuccessDispatchResult<int>(42);
        }

        public void Handle(MessageY message)
        {
            Assert.IsNotNull(message);
            Assert.AreEqual(34, message.I);

            if (message.Fail)
                throw new Exception("Fail");
        }

        public async Task<IDispatchResult> HandleAsync(MessageX message)
        {
            Assert.IsNotNull(message);

            await Task.Yield();

            return new SuccessDispatchResult<int>(42);
        }

        public async Task HandleAsync(MessageY message)
        {
            await Task.Yield();
            Assert.IsNotNull(message);
            Assert.AreEqual(34, message.I);
        }
    }

    public class MessageX { }

    public class MessageY
    {
        public int I { get; set; }
        public bool Fail { get; set; }
    }

    public class MessageZ { }

    public class ServiceA { }

    public class ServiceB { }
}
