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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging.MessageHandlers
{
    /// <summary>
    /// Contains factory methods to create generic message handler invoker.
    /// </summary>
    public static class MessageHandlerInvoker
    {
        private static readonly Type _messageHandlerInvokerTypeDefinition = typeof(MessageHandlerInvoker<>);
        private static readonly ConditionalWeakTable<Type, Func<object, MessageHandlerActionDescriptor, IEnumerable<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler>> _factories
            = new ConditionalWeakTable<Type, Func<object, MessageHandlerActionDescriptor, IEnumerable<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler>>();

        private static readonly ConditionalWeakTable<Type, Func<object, MessageHandlerActionDescriptor, IEnumerable<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler>>.CreateValueCallback _factoryBuilderCache
            = BuildFactory;

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
        /// and resolves its dependencies from <paramref name="serviceProvider"/>.
        /// </remarks>
        public static IMessageHandler CreateInvoker(
            MessageHandlerActionDescriptor memberDescriptor,
            IEnumerable<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var handlerType = memberDescriptor.MessageHandlerType;
            var handler = ActivatorUtilities.CreateInstance(serviceProvider, handlerType);
            Debug.Assert(handler != null);

            return CreateInvokerInternal(handler!, memberDescriptor, messageProcessors, serviceProvider);
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
            IEnumerable<IMessageProcessorRegistration> messageProcessors,
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
           IEnumerable<IMessageProcessorRegistration> processors,
           IServiceProvider serviceProvider)
        {
            var messageType = memberDescriptor.MessageType;
            var factory = _factories.GetValue(messageType, _factoryBuilderCache);
            return factory(handler, memberDescriptor, processors, serviceProvider);
        }

        private static Func<object, MessageHandlerActionDescriptor, IEnumerable<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler> BuildFactory(Type messageType)
        {
            var messageHandlerInvokerType = _messageHandlerInvokerTypeDefinition.MakeGenericType(messageType);
            var ctor = messageHandlerInvokerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                Type.DefaultBinder,
                types: new[] { typeof(object), typeof(MessageHandlerActionDescriptor), typeof(IEnumerable<IMessageProcessorRegistration>), typeof(IServiceProvider) },
                modifiers: null);

            Debug.Assert(ctor != null);

            var handlerParameter = Expression.Parameter(typeof(object), "handler");
            var memberDescriptorParameter = Expression.Parameter(typeof(MessageHandlerActionDescriptor), "memberDescriptor");
            var processorsParameter = Expression.Parameter(typeof(IEnumerable<IMessageProcessorRegistration>), "processors");
            var serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var ctorCall = Expression.New(ctor, handlerParameter, memberDescriptorParameter, processorsParameter, serviceProviderParameter);
            var convertedInvoker = Expression.Convert(ctorCall, typeof(IMessageHandler));
            var lambda = Expression.Lambda<Func<object, MessageHandlerActionDescriptor, IEnumerable<IMessageProcessorRegistration>, IServiceProvider, IMessageHandler>>(
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
            IEnumerable<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider) : base(messageProcessors, serviceProvider)
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

        #region IMessageHandler

        /// <inheritdoc/>
        public ValueTask<IDispatchResult> HandleAsync(
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            bool localDispatch,
            RouteEndPointScope remoteScope,
            CancellationToken cancellation)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            ValueTask<IDispatchResult> InvokeCoreAsync(DispatchDataDictionary<TMessage> dispatchDataCore)
            {
                return InvokeAsync(dispatchDataCore, publish, localDispatch, remoteScope, cancellation);
            }

            return InvokeChainAsync(
                _handler,
                _memberDescriptor,
                dispatchData,
                publish,
                localDispatch,
                remoteScope,
                InvokeCoreAsync,
                cancellation);
        }

        Type IMessageHandler.MessageType => typeof(TMessage);

        #endregion

        private async ValueTask<IDispatchResult> InvokeAsync(
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            bool isLocalDispatch,
            RouteEndPointScope remoteScope,
            CancellationToken cancellation)
        {
            IMessageDispatchContext? context = null;
            var contextDescriptor = MessageHandlerContextDescriptor.GetDescriptor(_handler.GetType());

            IMessageDispatchContext BuildContext()
            {
                return new MessageDispatchContext(
                    _serviceProvider,
                    dispatchData,
                    publish,
                    isLocalDispatch,
                    remoteScope);
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
            Debug.Assert(member != null);
            var invoker = TypeMemberInvoker.GetInvoker(member!);

            object? ResolveParameter(ParameterInfo parameter)
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
                else if (ParameterDefaultValue.TryGetDefaultValue(parameter, out var defaultValue))
                {
                    return _serviceProvider.GetService(parameter.ParameterType) ?? defaultValue;
                }
                else
                {
                    return _serviceProvider.GetRequiredService(parameter.ParameterType);
                }
            }

            object? result;
            var resultType = invoker.ReturnTypeDescriptor.ResultType;

            try
            {
                result = await invoker.InvokeAsync(
                    _handler, dispatchData.Message, ResolveParameter).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                return new FailureDispatchResult(exc);
            }

            if (result == null)
            {
                if (resultType == typeof(void))
                {
                    return new SuccessDispatchResult();
                }

                return new NotFoundDispatchResult();
            }

            if (result is IDispatchResult dispatchResult)
            {
                return dispatchResult;
            }

            if (resultType.IsAsyncEnumerable(out var elementType))
            {
                resultType = typeof(IEnumerable<>).MakeGenericType(elementType);
                result = await AsyncEnumerableEvaluator.EvaluateAsyncEnumerableAsync(
                    elementType, result, cancellation).ConfigureAwait(false);
            }

            return SuccessDispatchResult.FromResult(resultType, result);
        }
    }

    internal static class AsyncEnumerableEvaluator
    {
        private static readonly ConditionalWeakTable<Type, Func<object, CancellationToken, Task<object>>> _asyncEvaluationFunctions
          = new ConditionalWeakTable<Type, Func<object, CancellationToken, Task<object>>>();

        // Cache delegate for performance reasons.
        private static readonly ConditionalWeakTable<Type, Func<object, CancellationToken, Task<object>>>.CreateValueCallback _buildAsyncEvaluationFunction = BuildAsyncEvaluationFunction;

        private static readonly MethodInfo _evaluateAsyncEnumerableMethodDefinition = GetEvaluateAsyncEnumerableMethodDefinition();

        private static MethodInfo GetEvaluateAsyncEnumerableMethodDefinition()
        {
            var result = typeof(AsyncEnumerableEvaluator)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(p => p.Name == nameof(EvaluateAsyncEnumerableAsync) && p.IsGenericMethodDefinition)
                ?? throw new InvalidOperationException($"Unable to reflect method 'EvaluateAsyncEnumerable`1'.");

            Debug.Assert(result.GetGenericArguments().Length == 1);
            Debug.Assert(result.ReturnType == typeof(Task<object>));

            return result;
        }

        public static Task<object> EvaluateAsyncEnumerableAsync(
            Type elementType,
            object asyncEnumerable,
            CancellationToken cancellation)
        {
            var asyncEvaluationFunction = _asyncEvaluationFunctions
                .GetValue(elementType, _buildAsyncEvaluationFunction);

            return asyncEvaluationFunction(asyncEnumerable, cancellation);
        }

        private static Func<object, CancellationToken, Task<object>> BuildAsyncEvaluationFunction(Type elementType)
        {
            var evaluateAsyncEnumerableMethod = _evaluateAsyncEnumerableMethodDefinition.MakeGenericMethod(elementType);

            var asyncEnumerableParameter = Expression.Parameter(typeof(object), "asyncEnumerable");
            var cancellationParameter = Expression.Parameter(typeof(CancellationToken), "cancellation");
            var convertedAsyncEnumerable = Expression.Convert(asyncEnumerableParameter, typeof(IAsyncEnumerable<>).MakeGenericType(elementType));
            var call = Expression.Call(evaluateAsyncEnumerableMethod, convertedAsyncEnumerable, cancellationParameter);
            var lambda = Expression.Lambda<Func<object, CancellationToken, Task<object>>>(call, asyncEnumerableParameter, cancellationParameter);
            return lambda.Compile();
        }

        private static async Task<object> EvaluateAsyncEnumerableAsync<T>(
            IAsyncEnumerable<T> asyncEnumerable,
            CancellationToken cancellation)
        {
            return await asyncEnumerable.ToListAsync(cancellation).ConfigureAwait(false);
        }
    }
}
