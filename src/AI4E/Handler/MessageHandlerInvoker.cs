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
using AI4E.Validation;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Handler
{
    /// <summary>
    /// Contains factory methods to create generic message handler invoker.
    /// </summary>
    public static class MessageHandlerInvoker
    {
        private static readonly Type _messageHandlerInvokerTypeDefinition = typeof(MessageHandlerInvoker<>);
        private static readonly ConcurrentDictionary<Type, Func<object, MessageHandlerActionDescriptor, IList<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler>> _factories
            = new ConcurrentDictionary<Type, Func<object, MessageHandlerActionDescriptor, IList<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler>>();

        private static readonly Func<Type, Func<object, MessageHandlerActionDescriptor, IList<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler>> _factoryBuilderCache = BuildFactory;

        /// <summary>
        /// Creates a <see cref="MessageHandlerInvoker{TMessage}"/> from the specified parameters.
        /// </summary>
        /// <param name="memberDescriptor">The descriptor that specifies the message handler member.</param>
        /// <param name="messageProcessors">A collection of <see cref="IMessageProcessor"/>s to call.</param>
        /// <param name="serviceProvider">A <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <returns>The created <see cref="MessageHandlerInvoker{TMessage}"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageProcessors"/> or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// This overload creates the message handler specified by <paramref name="memberDescriptor"/>
        /// and resolved its depdendencies from <paramref name="serviceProvider"/>.
        /// </remarks>
        public static IMessageHandler CreateInvoker(
            MessageHandlerActionDescriptor memberDescriptor,
            IList<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var handlerType = memberDescriptor.MessageHandlerType;
            var handler = ActivatorUtilities.CreateInstance(serviceProvider, handlerType);
            Assert(handler != null);

            return CreateInvokerInternal(handler, memberDescriptor, messageProcessors, serviceProvider);
        }

        /// <summary>
        /// Creates a <see cref="MessageHandlerInvoker{TMessage}"/> from the specified parameters.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        /// <param name="memberDescriptor">The descriptor that specifies the message handler member.</param>
        /// <param name="messageProcessors">A collection of <see cref="IMessageProcessor"/>s to call.</param>
        /// <param name="serviceProvider">A <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <returns>The created <see cref="MessageHandlerInvoker{TMessage}"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="handler"/>, <paramref name="messageProcessors"/> or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="handler"/> has a different type as specified by <paramref name="memberDescriptor"/>.
        /// </exception>
        public static IMessageHandler CreateInvoker(
            object handler,
            MessageHandlerActionDescriptor memberDescriptor,
            IList<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (handler.GetType() != memberDescriptor.MessageHandlerType)
                throw new ArgumentException($"The object must be of type {memberDescriptor.MessageHandlerType}", nameof(handler));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            return CreateInvokerInternal(handler, memberDescriptor, messageProcessors, serviceProvider);
        }

        private static IMessageHandler CreateInvokerInternal(
           object handler,
           MessageHandlerActionDescriptor memberDescriptor,
           IList<IMessageProcessorRegistration> processors,
           IServiceProvider serviceProvider)
        {
            var messageType = memberDescriptor.MessageType;
            var factory = _factories.GetOrAdd(messageType, _factoryBuilderCache);
            return factory(handler, memberDescriptor, processors, serviceProvider);
        }

        private static Func<object, MessageHandlerActionDescriptor, IList<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler> BuildFactory(Type messageType)
        {
            var messageHandlerInvokerType = _messageHandlerInvokerTypeDefinition.MakeGenericType(messageType);
            var ctor = messageHandlerInvokerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                Type.DefaultBinder,
                types: new[] { typeof(object), typeof(MessageHandlerActionDescriptor), typeof(IList<IMessageProcessorRegistration>), typeof(IServiceProvider) },
                modifiers: null);

            Assert(ctor != null);

            var handlerParameter = Expression.Parameter(typeof(object), "handler");
            var memberDescriptorParameter = Expression.Parameter(typeof(MessageHandlerActionDescriptor), "memberDescriptor");
            var processorsParameter = Expression.Parameter(typeof(IList<IMessageProcessorRegistration>), "processors");
            var serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var ctorCall = Expression.New(ctor, handlerParameter, memberDescriptorParameter, processorsParameter, serviceProviderParameter);
            var convertedInvoker = Expression.Convert(ctorCall, typeof(IMessageHandler));
            var lambda = Expression.Lambda<Func<object, MessageHandlerActionDescriptor, IList<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler>>(
                convertedInvoker, handlerParameter, memberDescriptorParameter, processorsParameter, serviceProviderParameter);

            return lambda.Compile();
        }
    }

    /// <summary>
    /// Represents message handlers as <see cref="IMessageHandler{TMessage}"/>.
    /// </summary>
    /// <typeparam name="TMessage">The type of handled message.</typeparam>
    public sealed class MessageHandlerInvoker<TMessage> : InvokerBase<TMessage>, IMessageHandler<TMessage>
        where TMessage : class
    {
        private readonly object _handler;
        private readonly MessageHandlerActionDescriptor _memberDescriptor;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Creates a new instance of type <see cref="MessageHandlerInvoker"/>.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        /// <param name="memberDescriptor">The descriptor that specifies the message handler member.</param>
        /// <param name="messageProcessors">A collection of <see cref="IMessageProcessor"/>s to call.</param>
        /// <param name="serviceProvider">A <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="handler"/>, <paramref name="messageProcessors"/> or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="handler"/> has a different type as specified by <paramref name="memberDescriptor"/>.
        /// </exception>
        public MessageHandlerInvoker(
            object handler,
            MessageHandlerActionDescriptor memberDescriptor,
            IList<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider)
            : base(BuildMessageProcessors(messageProcessors), serviceProvider)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (handler.GetType() != memberDescriptor.MessageHandlerType)
                throw new ArgumentException($"The object must be of type {memberDescriptor.MessageHandlerType}", nameof(handler));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _handler = handler;
            _memberDescriptor = memberDescriptor;
            _serviceProvider = serviceProvider;
        }

        private static IList<IMessageProcessorRegistration> BuildMessageProcessors(
            IList<IMessageProcessorRegistration> messageProcessors)
        {
            if (messageProcessors.Any(p => p.MessageProcessorType == typeof(ValidationMessageProcessor)))
            {
                return messageProcessors;
            }

            var result = messageProcessors.ToList();
            result.Add(MessageProcessorRegistration.Create<ValidationMessageProcessor>());
            return result;
        }

        #region IMessageHandler

        /// <inheritdoc/>
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            ValueTask<IDispatchResult> InvokeCoreAsync(DispatchDataDictionary<TMessage> dispatchDataCore)
            {
                return InvokeAsync(dispatchDataCore, publish, localDispatch, cancellation);
            }

            return InvokeChainAsync(
                _handler, _memberDescriptor, dispatchData, publish, localDispatch, InvokeCoreAsync, cancellation);
        }

        /// <inheritdoc/>
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            if (!(dispatchData.Message is TMessage))
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{MessageType}'.");

            if (!(dispatchData is DispatchDataDictionary<TMessage> typedDispatchData))
            {
                typedDispatchData = new DispatchDataDictionary<TMessage>(dispatchData.Message as TMessage, dispatchData);
            }

            return HandleAsync(typedDispatchData, publish, localDispatch, cancellation);
        }

        /// <inheritdoc/>
        public Type MessageType => typeof(TMessage);

        #endregion

        private async ValueTask<IDispatchResult> InvokeAsync(
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            bool isLocalDispatch,
            CancellationToken cancellation)
        {
            IMessageDispatchContext context = null;
            var contextDescriptor = MessageHandlerContextDescriptor.GetDescriptor(_handler.GetType());

            IMessageDispatchContext BuildContext()
            {
                return new MessageDispatchContext(_serviceProvider, dispatchData, publish, isLocalDispatch);
            }

            if (contextDescriptor.CanSetContext)
            {
                context = BuildContext();
                contextDescriptor.SetContext(_handler, context);
            }

            if (contextDescriptor.CanSetDispatcher)
            {
                var dispatcher = _serviceProvider.GetRequiredService<IMessageDispatcher>();
                contextDescriptor.SetDispatcher(_handler, dispatcher);
            }

            var member = _memberDescriptor.Member;
            Assert(member != null);
            var invoker = HandlerActionInvoker.GetInvoker(member);

            object ResolveParameter(ParameterInfo parameter)
            {
                if (parameter.ParameterType == typeof(IServiceProvider))
                {
                    return _serviceProvider;
                }
                else if (parameter.ParameterType == typeof(CancellationToken))
                {
                    return cancellation;
                }
                else if (parameter.ParameterType == typeof(IMessageDispatchContext))
                {
                    if (context == null)
                    {
                        context = BuildContext();
                    }

                    return context;
                }
                else if (parameter.ParameterType == typeof(DispatchDataDictionary) ||
                         parameter.ParameterType == typeof(DispatchDataDictionary<TMessage>))
                {
                    return dispatchData;
                }
                else if (parameter.HasDefaultValue)
                {
                    return _serviceProvider.GetService(parameter.ParameterType) ?? parameter.DefaultValue;
                }
                else
                {
                    return _serviceProvider.GetRequiredService(parameter.ParameterType);
                }
            }

            object result;

            try
            {
                result = await invoker.InvokeAsync(_handler, dispatchData.Message, ResolveParameter);
            }
            catch (Exception exc)
            {
                return new FailureDispatchResult(exc);
            }

            if (result == null)
            {
                if (invoker.ReturnTypeDescriptor.ResultType == typeof(void))
                {
                    return new SuccessDispatchResult();
                }

                return new NotFoundDispatchResult();
            }

            if (result is IDispatchResult dispatchResult)
            {
                return dispatchResult;
            }

            return SuccessDispatchResult.FromResult(invoker.ReturnTypeDescriptor.ResultType, result);
        }
    }
}
