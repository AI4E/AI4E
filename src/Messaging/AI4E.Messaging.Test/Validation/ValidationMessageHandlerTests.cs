/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Handler;
using AI4E.Messaging.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Messaging.Validation
{
    [TestClass]
    public class ValidationMessageHandlerTests
    {
        [TestMethod]
        public async Task ValidateValidTest()
        {

            var memberDescriptor = new MessageHandlerActionDescriptor(
               typeof(ValidationTestMessage),
               typeof(ValidationTestMessageHandler),
               typeof(ValidationTestMessageHandler).GetMethod("Handle"));
            var configuration = memberDescriptor.BuildConfiguration();
            var handlerRegistration = new MessageHandlerRegistration(
                memberDescriptor.MessageType,
                configuration,
                provider => throw null,
                memberDescriptor);
            var messageHandlerProvider = new MessageHandlerProviderMock();
            messageHandlerProvider.GetHandlerRegistrations(typeof(ValidationTestMessage)).Add(handlerRegistration);
            var messageDispatcher = new MessageDispatcherMock { MessageHandlerProvider = messageHandlerProvider };
            var serviceProvider = BuildServiceProvider(services =>
            {
                services.AddSingleton<IMessageDispatcher>(messageDispatcher);
                services.AddOptions();
                services.Configure<MessagingOptions>(options =>
                {
                    options.MessageProcessors.Clear();
                });
            });
            var validationMessageHandler = ActivatorUtilities.CreateInstance<ValidationMessageHandler>(serviceProvider);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<Validate<ValidationTestMessage>>(new Validate<ValidationTestMessage>(testMessage));

            var dispatchResult = await validationMessageHandler.HandleAsync(dispatchData, publish: false, localDispatch: true, cancellationToken);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
        }

        [TestMethod]
        public async Task ValidateInvalidTest()
        {
            var memberDescriptor = new MessageHandlerActionDescriptor(
               typeof(ValidationTestMessage),
               typeof(ValidationTestMessageHandler),
               typeof(ValidationTestMessageHandler).GetMethod("Handle"));
            var configuration = memberDescriptor.BuildConfiguration();
            var handlerRegistration = new MessageHandlerRegistration(
                memberDescriptor.MessageType,
                configuration,
                provider => MessageHandlerInvoker.CreateInvoker(memberDescriptor, ImmutableArray<IMessageProcessorRegistration>.Empty, provider),
                memberDescriptor);
            var messageHandlerProvider = new MessageHandlerProviderMock();
            messageHandlerProvider.GetHandlerRegistrations(typeof(ValidationTestMessage)).Add(handlerRegistration);
            var messageDispatcher = new MessageDispatcherMock { MessageHandlerProvider = messageHandlerProvider };
            var serviceProvider = BuildServiceProvider(services =>
            {
                services.AddSingleton<IMessageDispatcher>(messageDispatcher);
                services.AddOptions();
                services.Configure<MessagingOptions>(options =>
                {
                    options.MessageProcessors.Clear();
                });
            });
            var validationMessageHandler = ActivatorUtilities.CreateInstance<ValidationMessageHandler>(serviceProvider);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var testMessage = new ValidationTestMessage("   ", -1);
            var dispatchData = new DispatchDataDictionary<Validate<ValidationTestMessage>>(new Validate<ValidationTestMessage>(testMessage));

            var dispatchResult = await validationMessageHandler.HandleAsync(dispatchData, publish: false, localDispatch: true, cancellationToken);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
        }

        [TestMethod]
        public async Task ValidationEnabledMessageProcessorTest()
        {
            var memberDescriptor = new MessageHandlerActionDescriptor(
               typeof(ValidationTestMessage),
               typeof(ValidationTestMessageHandler),
               typeof(ValidationTestMessageHandler).GetMethod("Handle"));
            var configuration = memberDescriptor.BuildConfiguration();
            var handlerRegistration = new MessageHandlerRegistration(
                memberDescriptor.MessageType,
                configuration,
                provider => MessageHandlerInvoker.CreateInvoker(memberDescriptor, ImmutableArray<IMessageProcessorRegistration>.Empty, provider),
                memberDescriptor);
            var messageHandlerProvider = new MessageHandlerProviderMock();
            messageHandlerProvider.GetHandlerRegistrations(typeof(ValidationTestMessage)).Add(handlerRegistration);
            var messageDispatcher = new MessageDispatcherMock { MessageHandlerProvider = messageHandlerProvider };
            var validationEnabledMessageProcessor = new ValidationEnabledMessageProcessor();
            var serviceProvider = BuildServiceProvider(services =>
            {
                services.AddSingleton<IMessageDispatcher>(messageDispatcher);
                services.AddOptions();
                services.Configure<MessagingOptions>(options =>
                {
                    options.MessageProcessors.Clear();
                    options.MessageProcessors.Add(new MessageProcessorRegistration(validationEnabledMessageProcessor));
                });
            });
            var validationMessageHandler = ActivatorUtilities.CreateInstance<ValidationMessageHandler>(serviceProvider);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var testMessage = new ValidationTestMessage("   ", -1);
            var dispatchData = new DispatchDataDictionary<Validate<ValidationTestMessage>>(new Validate<ValidationTestMessage>(testMessage));

            var dispatchResult = await validationMessageHandler.HandleAsync(dispatchData, publish: false, localDispatch: true, cancellationToken);

            Assert.IsTrue(validationEnabledMessageProcessor.Called);
            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
            {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
        }

        [TestMethod]
        public async Task ValidationDisabledMessageProcessorTest()
        {
            var memberDescriptor = new MessageHandlerActionDescriptor(
               typeof(ValidationTestMessage),
               typeof(ValidationTestMessageHandler),
               typeof(ValidationTestMessageHandler).GetMethod("Handle"));
            var configuration = memberDescriptor.BuildConfiguration();
            var handlerRegistration = new MessageHandlerRegistration(
                memberDescriptor.MessageType,
                configuration,
                provider => MessageHandlerInvoker.CreateInvoker(memberDescriptor, ImmutableArray<IMessageProcessorRegistration>.Empty, provider),
                memberDescriptor);
            var messageHandlerProvider = new MessageHandlerProviderMock();
            messageHandlerProvider.GetHandlerRegistrations(typeof(ValidationTestMessage)).Add(handlerRegistration);
            var messageDispatcher = new MessageDispatcherMock { MessageHandlerProvider = messageHandlerProvider };
            var validationEnabledMessageProcessor = new ValidationDisabledMessageProcessor();
            var serviceProvider = BuildServiceProvider(services =>
            {
                services.AddSingleton<IMessageDispatcher>(messageDispatcher);
                services.AddOptions();
                services.Configure<MessagingOptions>(options =>
                {
                    options.MessageProcessors.Clear();
                    options.MessageProcessors.Add(new MessageProcessorRegistration(validationEnabledMessageProcessor));
                });
            });
            var validationMessageHandler = ActivatorUtilities.CreateInstance<ValidationMessageHandler>(serviceProvider);
            using var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var testMessage = new ValidationTestMessage("   ", -1);
            var dispatchData = new DispatchDataDictionary<Validate<ValidationTestMessage>>(new Validate<ValidationTestMessage>(testMessage));

            var dispatchResult = await validationMessageHandler.HandleAsync(dispatchData, publish: false, localDispatch: true, cancellationToken);

            Assert.IsFalse(validationEnabledMessageProcessor.Called);
            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
            {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
        }

        private static IServiceProvider BuildServiceProvider(Action<IServiceCollection> config)
        {
            var services = new ServiceCollection();
            config(services);
            return services.BuildServiceProvider();
        }
    }

    [CallOnValidation]
    public class ValidationEnabledMessageProcessor : MessageProcessor
    {
        public override ValueTask<IDispatchResult> ProcessAsync<TMessage>(
            DispatchDataDictionary<TMessage> dispatchData,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
            CancellationToken cancellation)
        {
            Called = true;
            return base.ProcessAsync(dispatchData, next, cancellation);
        }

        public bool Called { get; private set; }
    }

    public class ValidationDisabledMessageProcessor : MessageProcessor
    {
        public override ValueTask<IDispatchResult> ProcessAsync<TMessage>(
            DispatchDataDictionary<TMessage> dispatchData,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
            CancellationToken cancellation)
        {
            Called = true;
            return base.ProcessAsync(dispatchData, next, cancellation);
        }

        public bool Called { get; private set; }
    }
}
