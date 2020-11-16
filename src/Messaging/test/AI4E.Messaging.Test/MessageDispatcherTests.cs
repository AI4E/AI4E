/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Mocks;
using AI4E.Messaging.Routing;
using AI4E.Messaging.Serialization;
using AI4E.Utils;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging
{
    [TestClass]
    public class MessageDispatcherTests
    {
        private static IOptions<MessagingOptions> BuildMessagingOptions()
        {
            return Options.Create(new MessagingOptions());
        }

        private static IMessageRouterFactory BuildMessageRouterFactory(
            IOptions<MessagingOptions> optionsProvider)
        {
            var routeManager = new RouteManager();
            var routingSystem = new RoutingSystem();

            return new MessageRouterFactory(routeManager, routingSystem, optionsProvider);
        }

        [TestMethod]
        public async Task DispatchTest()
        {
            var registry = new MessageHandlerRegistry();
            var desiredDispatchResult = new SuccessDispatchResult();
            var handler = new StringTestMessageHandler { DispatchResult = desiredDispatchResult };
            var handlerRegistration = new MessageHandlerRegistration<string>(p => { handler.ServiceProvider = p; return handler; });
            registry.Register(handlerRegistration);
            var serviceProvider = new ServiceProviderMock();

            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

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
            var optionsProvider = BuildMessagingOptions();
            var messageRouterFactory = BuildMessageRouterFactory(optionsProvider);
            var dispatcher = new MessageDispatcher(
                registry, messageRouterFactory, new MessageSerializer(TypeResolver.Default), TypeResolver.Default, serviceProvider, optionsProvider);

            var dispatchData = new DispatchDataDictionary<DerivedMessageType>(new DerivedMessageType());
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            var dispatchResult = await dispatcher.DispatchAsync(dispatchData, publish: false, cancellation);

            Assert.IsTrue(handler1.Called);
            Assert.IsFalse(handler2.Called);
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
            RouteEndPointScope remoteScope,
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
            RouteEndPointScope remoteScope,
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
            RouteEndPointScope remoteScope,
            CancellationToken cancellation)
        {
            Called = true;

            return new ValueTask<IDispatchResult>(DispatchResult);
        }

        public Type MessageType => typeof(DerivedMessageType);

        public IDispatchResult DispatchResult { get; set; }
        public bool Called { get; private set; }
    }

    public class ThrowExceptionTestMessageHandler : IMessageHandler<string>
    {
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<string> dispatchData,
            bool publish,
            bool localDispatch,
            RouteEndPointScope remoteScope,
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
            RouteEndPointScope remoteScope,
            CancellationToken cancellation)
        {
            throw null;
        }

        public Type MessageType => typeof(BaseMessageType);
    }
}
