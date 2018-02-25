/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MessageDispatcher.cs
 * Types:           (1) AI4E.MessageDispatcher
 *                  (2) AI4E.ITypedMessageDispatcher
 *                  (3) AI4E.MessageDispatcher'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   25.02.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E
{
    public sealed class MessageDispatcher : IMessageDispatcher
    {
        private static readonly Type _typedDispatcherType = typeof(MessageDispatcher<>);

        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<Type, ITypedMessageDispatcher> _typedDispatchers;

        public MessageDispatcher(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _typedDispatchers = new ConcurrentDictionary<Type, ITypedMessageDispatcher>();
            _serviceProvider = serviceProvider;
        }

        private MessageDispatcher<TMessage> GetTypedDispatcher<TMessage>()
        {
            return (MessageDispatcher<TMessage>)_typedDispatchers.GetOrAdd(typeof(TMessage), _ => new MessageDispatcher<TMessage>());
        }

        public IHandlerRegistration<IMessageHandler<TMessage>> Register<TMessage>(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            return GetTypedDispatcher<TMessage>().Register(messageHandlerProvider);
        }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return DispatchAsync(typeof(TMessage), message, context, publish, cancellation);
        }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, IServiceProvider serviceProvider, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            return DispatchAsync(typeof(TMessage), message, context, publish, serviceProvider, cancellation);
        }

        private ITypedMessageDispatcher GetTypedDispatcher(Type messageType)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            return _typedDispatchers.GetOrAdd(
                messageType,
                valueFactory: _ => (ITypedMessageDispatcher)Activator.CreateInstance(_typedDispatcherType.MakeGenericType(messageType)));
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation)
        {
            return DispatchAsync(messageType, message, context, publish, _serviceProvider, cancellation);
        }

        public async Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, IServiceProvider serviceProvider, CancellationToken cancellation)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var currType = messageType;
            var tasks = new List<Task<IDispatchResult>>();

            do
            {
                Debug.Assert(currType != null);

                if (TryGetTypedDispatcher(currType, out var dispatcher))
                {
                    tasks.Add(dispatcher.DispatchAsync(message, context, publish, serviceProvider, cancellation));

                    if (!publish)
                    {
                        break;
                    }
                }
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);

            if (tasks.Count == 0)
            {
                // When publishing a message and no handlers are available, this is a success.
                if (publish)
                {
                    return new SuccessDispatchResult();
                }

                // When dispatching a message and no handlers are available, this is a failure.
                return new DispatchFailureDispatchResult(messageType);
            }

            if (tasks.Count == 1)
            {
                return await tasks[0];
            }

            return new AggregateDispatchResult(await Task.WhenAll(tasks));
        }

        private bool TryGetTypedDispatcher(Type type, out ITypedMessageDispatcher typedDispatcher)
        {
            Debug.Assert(type != null);

            var result = _typedDispatchers.TryGetValue(type, out typedDispatcher);

            Debug.Assert(!result || typedDispatcher != null);
            Debug.Assert(!result || typedDispatcher.MessageType == type);
            return result;
        }
    }

    internal interface ITypedMessageDispatcher
    {
        Task<IDispatchResult> DispatchAsync(object message, DispatchValueDictionary context, bool publish, IServiceProvider serviceProviders, CancellationToken cancellation);

        Type MessageType { get; }
    }

    internal sealed class MessageDispatcher<TMessage> : ITypedMessageDispatcher
    {
        private readonly HandlerRegistry<IMessageHandler<TMessage>> _registry = new HandlerRegistry<IMessageHandler<TMessage>>();

        public IHandlerRegistration<IMessageHandler<TMessage>> Register(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            return _registry.CreateRegistration(messageHandlerProvider);
        }

        public async Task<IDispatchResult> DispatchAsync(TMessage message, DispatchValueDictionary context, bool publish, IServiceProvider serviceProvider, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (publish)
            {
                var dispatchResults = await Task.WhenAll(_registry.Handlers.Select(p => DispatchSingleHandlerAsync(p, message, context, serviceProvider, cancellation)));

                return new AggregateDispatchResult(dispatchResults);
            }
            else
            {
                if (_registry.TryGetHandler(out var handler))
                {
                    return await DispatchSingleHandlerAsync(handler, message, context, serviceProvider, cancellation);
                }

                return new DispatchFailureDispatchResult(typeof(TMessage));
            }
        }

        private Task<IDispatchResult> DispatchSingleHandlerAsync(IContextualProvider<IMessageHandler<TMessage>> handler, TMessage message, DispatchValueDictionary context, IServiceProvider serviceProvider, CancellationToken cancellation)
        {
            // TODO: Cancellation

            Debug.Assert(message != null);
            Debug.Assert(handler != null);
            Debug.Assert(serviceProvider != null);

            using (var scope = serviceProvider.CreateScope())
            {
                try
                {
                    return handler.ProvideInstance(scope.ServiceProvider).HandleAsync(message, context);
                }
                catch (ConcurrencyException)
                {
                    return Task.FromResult<IDispatchResult>(new ConcurrencyIssueDispatchResult());
                }
                catch (Exception exc)
                {
                    return Task.FromResult<IDispatchResult>(new FailureDispatchResult(exc));
                }
            }
        }

        public Task<IDispatchResult> DispatchAsync(object message, DispatchValueDictionary context, bool publish, IServiceProvider serviceProvider, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (!(message is TMessage typedMessage))
            {
                throw new ArgumentException($"The argument must be of type '{ typeof(TMessage).FullName }' or a derived type.", nameof(message));
            }

            return DispatchAsync(typedMessage, context, publish, serviceProvider, cancellation);
        }

        public Type MessageType => typeof(TMessage);
    }
}
