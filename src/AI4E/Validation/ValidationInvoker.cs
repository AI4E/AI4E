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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Handler;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Validation
{
    public static class ValidationInvoker
    {
        private static readonly ConcurrentDictionary<Type, InvokerFactory> _factories =
            new ConcurrentDictionary<Type, InvokerFactory>();

        private static readonly Type _validationInvokerTypeDefinition = typeof(ValidationInvoker<>);

        public static IValidationInvoker CreateInvoker(
            Type messageType,
            IList<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            var factory = GetFactory(messageType);
            return factory(messageProcessors, serviceProvider);
        }

        private static InvokerFactory GetFactory(Type messageType)
        {
            return _factories.GetOrAdd(messageType, BuildFactory);
        }

        private static InvokerFactory BuildFactory(Type messageType)
        {
            var validationInvokerType = _validationInvokerTypeDefinition.MakeGenericType(messageType);
            var ctor = validationInvokerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public, Type.DefaultBinder,
                new[] { typeof(IList<IMessageProcessorRegistration>), typeof(IServiceProvider) },
                modifiers: null);
            var messageProcessorsParameter = Expression.Parameter(
                typeof(IList<IMessageProcessorRegistration>), "messageProcessors");
            var serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var instanciation = Expression.New(ctor, messageProcessorsParameter, serviceProviderParameter);
            var lambda = Expression.Lambda<InvokerFactory>(
                instanciation, messageProcessorsParameter, serviceProviderParameter);
            return lambda.Compile();
        }

        private delegate IValidationInvoker InvokerFactory(
            IList<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider);
    }

    public interface IValidationInvoker
    {
        ValueTask<IDispatchResult> InvokeValidationAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            IMessageHandlerRegistration handlerRegistration,
            CancellationToken cancellation);
    }

    public sealed class ValidationInvoker<TMessage> : InvokerBase<TMessage>, IValidationInvoker
        where TMessage : class
    {
        private readonly IServiceProvider _serviceProvider;

        public ValidationInvoker(
            IList<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider) : base(messageProcessors, serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public ValueTask<IDispatchResult> InvokeValidationAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            IMessageHandlerRegistration handlerRegistration,
            CancellationToken cancellation)
        {
            if (!(dispatchData.Message is TMessage))
                throw new InvalidOperationException(
                    $"Cannot dispatch a message of type '{dispatchData.MessageType}' to " +
                    $"a handler that handles messages of type '{typeof(TMessage)}'.");

            if (!(dispatchData is DispatchDataDictionary<TMessage> typedDispatchData))
            {
                typedDispatchData = new DispatchDataDictionary<TMessage>(
                    dispatchData.Message as TMessage, dispatchData);
            }

            return InvokeValidationAsync(
                typedDispatchData, publish, localDispatch, handlerRegistration, cancellation);
        }

        public ValueTask<IDispatchResult> InvokeValidationAsync(
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            bool localDispatch,
            IMessageHandlerRegistration handlerRegistration,
            CancellationToken cancellation)
        {
            // The handler has no descriptor and cannot have a validation therefore.
            if (!handlerRegistration.TryGetDescriptor(out var descriptor))
            {
                return new ValueTask<IDispatchResult>(new SuccessDispatchResult());
            }

            var parameterType = ValidationMessageProcessor.GetMessageHandlerMessageType(descriptor);

            // The handler has no validation.
            if (!ValidationMessageProcessor.TryGetDescriptor(
                descriptor.MessageHandlerType, parameterType, out var validation))
            {
                return new ValueTask<IDispatchResult>(new SuccessDispatchResult());
            }

            var handlerType = descriptor.MessageHandlerType;
            var handler = ActivatorUtilities.CreateInstance(_serviceProvider, handlerType);

            ValueTask<IDispatchResult> InvokeValidation(DispatchDataDictionary<TMessage> nextDispatchData)
            {
                var invokeResult = ValidationMessageProcessor.InvokeValidationAsync(
                    handler,
                    validation,
                    nextDispatchData,
                    _serviceProvider,
                    cancellation);

                return EvaluateValidationInvokation(invokeResult);
            }

            return InvokeChainAsync(
                handler, descriptor, dispatchData, publish, localDispatch, InvokeValidation, cancellation);
        }

        protected override bool ExecuteProcessor(IMessageProcessorRegistration messageProcessorRegistration)
        {
            var processorType = messageProcessorRegistration.MessageProcessorType;

            var callOnValidationAttribute = processorType
                .GetCustomAttribute<CallOnValidationAttribute>(inherit: true);

            return callOnValidationAttribute != null && callOnValidationAttribute.CallOnValidation;
        }

        private async ValueTask<IDispatchResult> EvaluateValidationInvokation(
            ValueTask<IEnumerable<ValidationResult>> invokeResult)
        {
            var validationResults = await invokeResult;

            if (validationResults.Any())
            {
                return new ValidationFailureDispatchResult(validationResults);
            }
            else
            {
                return new SuccessDispatchResult();
            }
        }
    }
}
