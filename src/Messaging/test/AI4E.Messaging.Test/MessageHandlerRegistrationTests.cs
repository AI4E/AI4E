using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class MessageHandlerRegistrationTests
    {
        [TestMethod]
        public void CreateMessageTypeAndFactoryTest()
        {
            var messageHandlerRegistration = new MessageHandlerRegistration(
                typeof(string), provider => new MessageHandlerRegistrationTestMessagHandler(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var handler = (MessageHandlerRegistrationTestMessagHandler)messageHandlerRegistration.CreateMessageHandler(serviceProvider);

            Assert.AreEqual(0, messageHandlerRegistration.Configuration.GetInternalData()?.Count ?? 0);
            Assert.AreSame(typeof(string), messageHandlerRegistration.MessageType);
            Assert.AreSame(serviceProvider, handler.ServiceProvider);
        }

        [TestMethod]
        public void CreateMessageTypeAndFactory2Test()
        {
            var messageHandlerRegistration = new MessageHandlerRegistration(
                typeof(object), provider => new MessageHandlerRegistrationTestMessagHandler(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                messageHandlerRegistration.CreateMessageHandler(serviceProvider);
            });
        }

        [TestMethod]
        public void CreateMessageTypeAndFactory3Test()
        {
            var messageHandlerRegistration = new MessageHandlerRegistration(typeof(object), provider => null);
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                messageHandlerRegistration.CreateMessageHandler(serviceProvider);
            });
        }

        [TestMethod]
        public void CreateMessageTypeConfigurationAndFactoryTest()
        {
            var config = new Dictionary<Type, object>
            {
                [typeof(HandlerConfiguration1)] = new HandlerConfiguration1()
            };
            var messageHandlerRegistration = new MessageHandlerRegistration(
                typeof(string),
                new MessageHandlerConfiguration(config.ToImmutableDictionary()),
                provider => new MessageHandlerRegistrationTestMessagHandler(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var handler = (MessageHandlerRegistrationTestMessagHandler)messageHandlerRegistration.CreateMessageHandler(serviceProvider);

            Assert.AreEqual(1, messageHandlerRegistration.Configuration.GetInternalData()?.Count ?? 0);
            Assert.IsTrue(config.ToHashSet().SetEquals(messageHandlerRegistration.Configuration.GetInternalData()));
            Assert.AreSame(typeof(string), messageHandlerRegistration.MessageType);
            Assert.AreSame(serviceProvider, handler.ServiceProvider);
        }

        [TestMethod]
        public void CreateMessageTypeConfigurationAndFactory2Test()
        {
            var config = new Dictionary<Type, object>
            {
                [typeof(HandlerConfiguration1)] = new HandlerConfiguration1()
            };
            var messageHandlerRegistration = new MessageHandlerRegistration(
                typeof(object),
                new MessageHandlerConfiguration(config.ToImmutableDictionary()),
                provider => new MessageHandlerRegistrationTestMessagHandler(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.AreEqual(1, messageHandlerRegistration.Configuration.GetInternalData()?.Count ?? 0);
            Assert.IsTrue(config.ToHashSet().SetEquals(messageHandlerRegistration.Configuration.GetInternalData()));
            Assert.AreSame(typeof(object), messageHandlerRegistration.MessageType);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                messageHandlerRegistration.CreateMessageHandler(serviceProvider);
            });
        }

        [TestMethod]
        public void CreateMessageTypeConfigurationAndFactory3Test()
        {
            var config = new Dictionary<Type, object>
            {
                [typeof(HandlerConfiguration1)] = new HandlerConfiguration1()
            };
            var messageHandlerRegistration = new MessageHandlerRegistration(
                typeof(object),
                new MessageHandlerConfiguration(config.ToImmutableDictionary()),
                provider => null);
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.AreEqual(1, messageHandlerRegistration.Configuration.GetInternalData()?.Count ?? 0);
            Assert.IsTrue(config.ToHashSet().SetEquals(messageHandlerRegistration.Configuration.GetInternalData()));
            Assert.AreSame(typeof(object), messageHandlerRegistration.MessageType);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                messageHandlerRegistration.CreateMessageHandler(serviceProvider);
            });
        }

        [TestMethod]
        public void GenericCreateAndFactoryTest()
        {
            var messageHandlerRegistration = new MessageHandlerRegistration<string>(
                provider => new MessageHandlerRegistrationTestMessagHandler(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var handler = (MessageHandlerRegistrationTestMessagHandler)messageHandlerRegistration.CreateMessageHandler(serviceProvider);

            Assert.AreEqual(0, messageHandlerRegistration.Configuration.GetInternalData()?.Count ?? 0);
            Assert.AreSame(typeof(string), ((IMessageHandlerRegistration)messageHandlerRegistration).MessageType);
            Assert.AreSame(serviceProvider, handler.ServiceProvider);
        }

        [TestMethod]
        public void GenericCreateAndFactory2Test()
        {
            var messageHandlerRegistration = new MessageHandlerRegistration<object>(provider => null);
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                messageHandlerRegistration.CreateMessageHandler(serviceProvider);
            });
        }

        [TestMethod]
        public void GenericCreateConfigurationAndFactoryTest()
        {
            var config = new Dictionary<Type, object>
            {
                [typeof(HandlerConfiguration1)] = new HandlerConfiguration1()
            };
            var messageHandlerRegistration = new MessageHandlerRegistration<string>(
                new MessageHandlerConfiguration(config.ToImmutableDictionary()),
                provider => new MessageHandlerRegistrationTestMessagHandler(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var handler = (MessageHandlerRegistrationTestMessagHandler)messageHandlerRegistration.CreateMessageHandler(serviceProvider);

            Assert.AreEqual(1, messageHandlerRegistration.Configuration.GetInternalData()?.Count ?? 0);
            Assert.IsTrue(config.ToHashSet().SetEquals(messageHandlerRegistration.Configuration.GetInternalData()));
            Assert.AreSame(typeof(string), ((IMessageHandlerRegistration)messageHandlerRegistration).MessageType);
            Assert.AreSame(serviceProvider, handler.ServiceProvider);
        }

        [TestMethod]
        public void GenericCreateConfigurationAndFactory2Test()
        {
            var config = new Dictionary<Type, object>
            {
                [typeof(HandlerConfiguration1)] = new HandlerConfiguration1()
            };
            var messageHandlerRegistration = new MessageHandlerRegistration<object>(
                new MessageHandlerConfiguration(config.ToImmutableDictionary()),
                provider => null);
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.AreEqual(1, messageHandlerRegistration.Configuration.GetInternalData()?.Count ?? 0);
            Assert.IsTrue(config.ToHashSet().SetEquals(messageHandlerRegistration.Configuration.GetInternalData()));
            Assert.AreSame(typeof(object), ((IMessageHandlerRegistration)messageHandlerRegistration).MessageType);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                messageHandlerRegistration.CreateMessageHandler(serviceProvider);
            });
        }
    }

    public sealed class MessageHandlerRegistrationTestMessagHandler : IMessageHandler<string>
    {
        public MessageHandlerRegistrationTestMessagHandler(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public ValueTask<IDispatchResult> HandleAsync(DispatchDataDictionary<string> dispatchData, bool publish, bool localDispatch, CancellationToken cancellation)
        {
            throw null;
        }

        public ValueTask<IDispatchResult> HandleAsync(DispatchDataDictionary dispatchData, bool publish, bool localDispatch, CancellationToken cancellation)
        {
            throw null;
        }

        public Type MessageType => typeof(string);

        public IServiceProvider ServiceProvider { get; }
    }
}
