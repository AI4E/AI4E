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
using AI4E.Messaging.MessageHandlers;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging.Validation
{
    /// <summary>
    /// Contains factory methods to create generic validation invoker.
    /// </summary>
    public static class ValidationInvoker
    {
        private static readonly ConcurrentDictionary<Type, InvokerFactory> _factories =
            new ConcurrentDictionary<Type, InvokerFactory>();

        private static readonly Type _validationInvokerTypeDefinition = typeof(ValidationInvoker<>);

        /// <summary>
        /// Creates a <see cref="ValidationInvoker{TMessage}"/> from the specified parameters.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <param name="messageProcessors">A collection of <see cref="IMessageProcessor"/>s to call.</param>
        /// <param name="serviceProvider">A <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <returns>The creates <see cref="ValidationInvoker{TMessage}"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="messageType"/>, <paramref name="messageProcessors"/>
        /// or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        public static IValidationInvoker CreateInvoker(
            Type messageType,
            IEnumerable<IMessageProcessorRegistration> messageProcessors,
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
                new[] { typeof(IEnumerable<IMessageProcessorRegistration>), typeof(IServiceProvider) },
                modifiers: null);
            var messageProcessorsParameter = Expression.Parameter(
                typeof(IEnumerable<IMessageProcessorRegistration>), "messageProcessors");
            var serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var instanciation = Expression.New(ctor, messageProcessorsParameter, serviceProviderParameter);
            var lambda = Expression.Lambda<InvokerFactory>(
                instanciation, messageProcessorsParameter, serviceProviderParameter);
            return lambda.Compile();
        }

        private delegate IValidationInvoker InvokerFactory(
            IEnumerable<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider);
    }

    /// <summary>
    /// Represents a validation invoker.
    /// </summary>
    public interface IValidationInvoker
    {
        /// <summary>
        /// Invokes the validation with the specified parameters.
        /// </summary>
        /// <param name="dispatchData">The dispatch data that contains the message to handle and supporting data.</param>
        /// <param name="publish">A boolean value specifying whether the message is published to all handlers.</param>
        /// <param name="localDispatch">A boolean value specifying whether the message is dispatched locally.</param>
        /// <param name="handlerRegistration">The message handler that is responsible for handling the messages in non-validation dispatches.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either<paramref name="dispatchData"/> or <paramref name="handlerRegistration"/> is null.
        /// </exception>
        ValueTask<IDispatchResult> InvokeValidationAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            IMessageHandlerRegistration handlerRegistration,
            CancellationToken cancellation);
    }

    /// <summary>
    /// Represents a validation invoker.
    /// </summary>
    /// <typeparam name="TMessage">The type of message that is validated.</typeparam>
    public sealed class ValidationInvoker<TMessage> : InvokerBase<TMessage>, IValidationInvoker
        where TMessage : class
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="ValidationInvoker{TMessage}"/> type.
        /// </summary>
        /// <param name="messageProcessors">A collection of <see cref="IMessageProcessor"/>s to call.</param>
        /// <param name="serviceProvider">>A <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageProcessors"/> or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        public ValidationInvoker(
            IEnumerable<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider) : base(messageProcessors, serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public ValueTask<IDispatchResult> InvokeValidationAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            IMessageHandlerRegistration handlerRegistration,
            CancellationToken cancellation)
        {
            return InvokeValidationAsync(
                 dispatchData.As<TMessage>(), publish, localDispatch, handlerRegistration, cancellation);
        }

        /// <summary>
        /// Invokes the validation with the specified parameters.
        /// </summary>
        /// <param name="dispatchData">The dispatch data that contains the message to handle and supporting data.</param>
        /// <param name="publish">A boolean value specifying whether the message is published to all handlers.</param>
        /// <param name="localDispatch">A boolean value specifying whether the message is dispatched locally.</param>
        /// <param name="handlerRegistration">The message handler that is responsible for handling the messages in non-validation dispatches.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either<paramref name="dispatchData"/> or <paramref name="handlerRegistration"/> is null.
        /// </exception>
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

            // The handler has no validation.
            if (!descriptor.TryGetValidationDescriptor(out var validationDescriptor))
            {
                return new ValueTask<IDispatchResult>(new SuccessDispatchResult());
            }

            var handlerType = descriptor.MessageHandlerType;
            var handler = ActivatorUtilities.CreateInstance(_serviceProvider, handlerType);

            ValueTask<IDispatchResult> InvokeValidation(DispatchDataDictionary<TMessage> nextDispatchData)
            {
                var invokeResult = ValidationEvaluator.EvaluateAsync(
                    handler,
                    validationDescriptor,
                    nextDispatchData,
                    _serviceProvider,
                    cancellation);

                return EvaluateValidationInvokation(invokeResult);
            }

            return InvokeChainAsync(
                handler, descriptor, dispatchData, publish, localDispatch, InvokeValidation, cancellation);
        }

        /// <inheritdoc/>
        protected override bool ExecuteProcessor(IMessageProcessorRegistration messageProcessorRegistration)
        {
            return messageProcessorRegistration.CallOnValidation();
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
