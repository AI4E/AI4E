/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2019 - 2020 Andreas Truetschel and contributors.
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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Mocks;
using AI4E.Messaging.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.MessageHandlers
{
    [TestClass]
    public class MessageHandlerInvokerTests
    {
        [TestMethod]
        public void BuildTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ResolveServicesHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);

            Assert.AreEqual(typeof(string), ((IMessageHandler)messageHandler).MessageType);
        }

        [TestMethod]
        public async Task ResolveServicesTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ResolveServicesHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, cancellationToken);

            Assert.IsNotNull(handler.Context);
            Assert.AreEqual(serviceProvider, handler.Context.DispatchServices);
            Assert.AreEqual(dispatchData, handler.Context.DispatchData);
            Assert.AreEqual(true, handler.Context.IsPublish);
            Assert.AreEqual(true, handler.Context.IsLocalDispatch);
            Assert.AreSame(serviceProvider.GetRequiredService<IMessageDispatcher>(), handler.MessageDispatcher);
            Assert.AreEqual("abc", handler.Message);
            Assert.AreSame(dispatchData, handler.DispatchData);
            Assert.AreSame(dispatchData, handler.GenericDispatchData);
            Assert.IsNull(handler.WronglyTypedDispatchData);
            Assert.IsTrue(cancellationToken.Equals(handler.Cancellation));
            Assert.AreSame(handler.Context, handler.MessageDispatchContext);
            Assert.AreSame(serviceProvider.GetRequiredService<IService>(), handler.Service);
        }

        //[TestMethod] // https://github.com/AI4E/AI4E/issues/140
        public async Task ResolveUnresolvableRequiredServiceTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ResolveUnresolvableRequiredServiceHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);
            });
        }

        [TestMethod]
        public async Task ResolveUnresolvableNonRequiredServiceTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ResolveUnresolvableNonRequiredServiceHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);

            Assert.IsNull(handler.Service);
        }

        [TestMethod]
        public async Task ReturningDispatchResultTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ReturningDispatchResultHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);

            Assert.AreSame(handler.Result, result);
        }

        [TestMethod]
        public async Task ReturningDispatchResultCastedTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ReturningDispatchResultCastedHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);

            Assert.AreSame(handler.Result, result);
        }

        [TestMethod]
        public async Task ThrowTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ThrowHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);

            Assert.IsInstanceOfType(result, typeof(FailureDispatchResult));
            Assert.IsInstanceOfType(((FailureDispatchResult)result).Exception, typeof(InvalidOperationException));
        }

        [TestMethod]
        public async Task VoidResultTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.VoidResultHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);

            Assert.IsInstanceOfType(result, typeof(SuccessDispatchResult));
        }

        [TestMethod]
        public async Task TypeResultTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.TypeResultHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);

            Assert.IsInstanceOfType(result, typeof(SuccessDispatchResult<int>));
            Assert.AreEqual(5, ((SuccessDispatchResult<int>)result).Result);
        }

        [TestMethod]
        public async Task NonGenericHandleTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.TypeResultHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            var result = await ((IMessageHandler)messageHandler).HandleAsync((DispatchDataDictionary)dispatchData, true, true, remoteScope: default, default);

            Assert.IsInstanceOfType(result, typeof(SuccessDispatchResult<int>));
            Assert.AreEqual(5, ((SuccessDispatchResult<int>)result).Result);
        }

        [TestMethod]
        public async Task MessageProcessorTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var dispatchDataReplacement = new DispatchDataDictionary<string>("def");
            var dispatchResultReplacement = new FailureDispatchResult();
            var processor = new TestMessageProcessor(dispatchDataReplacement, dispatchResultReplacement);
            var processors = new[]
            {
                new MessageProcessorRegistration(processor)
            }.ToImmutableArray<IMessageProcessorRegistration>();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ReturningDispatchResultHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, cancellationToken);

            Assert.AreSame(dispatchResultReplacement, result);
            Assert.AreSame(dispatchDataReplacement, handler.Context.DispatchData);
            Assert.IsTrue(cancellationToken.Equals(processor.Cancellation));
            Assert.AreSame(dispatchData, processor.DispatchData);
            Assert.AreSame(handler.Result, processor.Result);

            Assert.AreEqual(memberDescriptor.MessageType, processor.Context.MessageHandlerAction.MessageType);
            Assert.AreEqual(memberDescriptor.MessageHandlerType, processor.Context.MessageHandlerAction.MessageHandlerType);
            Assert.AreEqual(memberDescriptor.Member, processor.Context.MessageHandlerAction.Member);
            Assert.AreSame(handler, processor.Context.MessageHandler);
            Assert.AreEqual(true, processor.Context.IsPublish);
            Assert.AreEqual(true, processor.Context.IsLocalDispatch);
        }

        [TestMethod]
        public async Task MessageProcessorOrderTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var resultList = new List<int>();

            var processor1 = new OrderTestMessageProcessor(resultList, 1);
            var processor2 = new OrderTestMessageProcessor(resultList, 2);
            var processors = new[]
            {
                new MessageProcessorRegistration(processor1),
                new MessageProcessorRegistration(processor2)
            }.ToImmutableArray<IMessageProcessorRegistration>();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ReturningDispatchResultHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, cancellationToken);

            Assert.IsTrue(resultList.SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public async Task MessageProcessorDispatchDataChainTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var resultList = new List<int>();

            var processor1 = new OrderTestMessageProcessor(resultList, 1);
            var processor2 = new OrderTestMessageProcessor(resultList, 2);
            var processors = new[]
            {
                new MessageProcessorRegistration(processor1),
                new MessageProcessorRegistration(processor2)
            }.ToImmutableArray<IMessageProcessorRegistration>();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.ReturningDispatchResultHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = new MessageHandlerInvoker<string>(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, cancellationToken);

            Assert.IsNull(processor1.DispatchData["processorIndex"]);
            Assert.AreEqual(1, processor2.DispatchData["processorIndex"]);
            Assert.AreEqual(2, handler.Context.DispatchData["processorIndex"]);
        }

        [TestMethod]
        public async Task CreateInvokerWithHandlerTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.TypeResultHandle)));
            var handler = new TestMessageHandler();
            var messageHandler = MessageHandlerInvoker.CreateInvoker(handler, memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);

            Assert.IsInstanceOfType(result, typeof(SuccessDispatchResult<int>));
            Assert.AreEqual(5, ((SuccessDispatchResult<int>)result).Result);
        }

        [TestMethod]
        public async Task CreateInvokerTest()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IService, Service>();
            serviceCollection.AddSingleton<IMessageDispatcher, MessageDispatcherMock>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processors = ImmutableArray<IMessageProcessorRegistration>.Empty;
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(string),
                typeof(TestMessageHandler),
                typeof(TestMessageHandler).GetMethod(nameof(TestMessageHandler.TypeResultHandle)));
            var messageHandler = MessageHandlerInvoker.CreateInvoker(memberDescriptor, processors, serviceProvider);
            var dispatchData = new DispatchDataDictionary<string>("abc");

            var result = await messageHandler.HandleAsync(dispatchData, true, true, remoteScope: default, default);

            Assert.IsInstanceOfType(result, typeof(SuccessDispatchResult<int>));
            Assert.AreEqual(5, ((SuccessDispatchResult<int>)result).Result);
        }

        // TODO: Test IMessageProcessorContext.MessageHandlerConfiguration
    }

    public sealed class TestMessageHandler
    {
        public void ResolveServicesHandle(
            string message,
            DispatchDataDictionary dispatchData,
            DispatchDataDictionary<string> genericDispatchData,
            CancellationToken cancellation,
            IMessageDispatchContext messageDispatchContext,
            IServiceProvider serviceProvider,
            IService service,
            DispatchDataDictionary<TestMessageHandler> wronglyTypedDispatchData = null)
        {
            Message = message;
            DispatchData = dispatchData;
            GenericDispatchData = genericDispatchData;
            WronglyTypedDispatchData = wronglyTypedDispatchData;
            Cancellation = cancellation;
            MessageDispatchContext = messageDispatchContext;
            ServiceProvider = serviceProvider;
            Service = service;
        }

        public void ResolveUnresolvableRequiredServiceHandle(string message, IService service)
        {
            Message = message;
            Service = service;
        }

        public void ResolveUnresolvableNonRequiredServiceHandle(string message, IService service = null)
        {
            Message = message;
            Service = service;
        }

        public IDispatchResult ReturningDispatchResultHandle(string message)
        {
            Result = new ValidationFailureDispatchResult();

            return (IDispatchResult)Result;
        }

        public object ReturningDispatchResultCastedHandle(string message)
        {
            Result = new ValidationFailureDispatchResult();

            return Result;
        }

        public void ThrowHandle(string message)
        {
            throw new InvalidOperationException();
        }

        public void VoidResultHandle(string message) { }

        public int TypeResultHandle(string message)
        {
            return 5;
        }

        public object Result { get; set; }

        [MessageDispatchContext]
        public IMessageDispatchContext Context { get; set; }

        [MessageDispatcher]
        public IMessageDispatcher MessageDispatcher { get; set; }

        public string Message { get; set; }
        public DispatchDataDictionary DispatchData { get; set; }
        public DispatchDataDictionary<string> GenericDispatchData { get; set; }
        public DispatchDataDictionary<TestMessageHandler> WronglyTypedDispatchData { get; set; }
        public CancellationToken Cancellation { get; set; }
        public IMessageDispatchContext MessageDispatchContext { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        public IService Service { get; set; }
    }

    public sealed class TestMessageProcessor : IMessageProcessor
    {
        public TestMessageProcessor(
            DispatchDataDictionary dispatchDataReplacement,
            IDispatchResult dispatchResultReplacement)
        {
            DispatchDataReplacement = dispatchDataReplacement;
            DispatchResultReplacement = dispatchResultReplacement;
        }

        public async ValueTask<IDispatchResult> ProcessAsync<TMessage>(
            DispatchDataDictionary<TMessage> dispatchData,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
            CancellationToken cancellation)
            where TMessage : class
        {
            DispatchData = dispatchData;
            Cancellation = cancellation;
            Result = await next((DispatchDataDictionary<TMessage>)DispatchDataReplacement);
            return DispatchResultReplacement;
        }

        public IDispatchResult Result { get; private set; }
        public DispatchDataDictionary DispatchData { get; private set; }
        public CancellationToken Cancellation { get; private set; }
        public DispatchDataDictionary DispatchDataReplacement { get; }
        public IDispatchResult DispatchResultReplacement { get; }

        [MessageProcessorContext]
        public IMessageProcessorContext Context { get; internal set; }
    }

    public sealed class OrderTestMessageProcessor : IMessageProcessor
    {
        private readonly List<int> _resultList;
        private readonly int _index;

        public OrderTestMessageProcessor(List<int> resultList, int index)
        {
            _resultList = resultList;
            _index = index;
        }

        public ValueTask<IDispatchResult> ProcessAsync<TMessage>(
            DispatchDataDictionary<TMessage> dispatchData,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
            CancellationToken cancellation)
            where TMessage : class
        {
            _resultList.Add(_index);
            DispatchData = dispatchData;

            var dispatchDataValues = new Dictionary<string, object>(dispatchData)
            {
                ["processorIndex"] = _index
            };

            return next(new DispatchDataDictionary<TMessage>(dispatchData.Message, dispatchDataValues));
        }

        public DispatchDataDictionary DispatchData { get; private set; }
    }

    public interface IService { }

    public class Service : IService { }
}
