/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MessageDispatcher.cs
 * Types:           (1) AI4E.MessageDispatcher
 *                  (2) AI4E.MessageDispatcher.ITypedMessageDispatcher
 *                  (3) AI4E.MessageDispatcher.TypedMessageDispatcher'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   09.09.2018 
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E
{
    public sealed class MessageDispatcher : IMessageDispatcher
    {
        #region Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<Type, ITypedMessageDispatcher> _typedDispatchers;

        #endregion

        #region C'tor

        public MessageDispatcher(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _typedDispatchers = new ConcurrentDictionary<Type, ITypedMessageDispatcher>();
            _serviceProvider = serviceProvider;
        }

        #endregion

        #region IMessageDispatcher

        public IHandlerRegistration<IMessageHandler<TMessage>> Register<TMessage>(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
            where TMessage : class
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            return GetTypedDispatcher<TMessage>().Register(messageHandlerProvider);
        }

        public async Task<IDispatchResult> DispatchAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            var currType = dispatchData.MessageType;
            var tasks = new List<Task<(IDispatchResult result, bool handlersFound)>>();

            do
            {
                Assert(currType != null);

                if (TryGetTypedDispatcher(currType, out var dispatcher))
                {
                    var dispatchOperation = dispatcher.DispatchAsync(dispatchData, publish, cancellation);

                    if (publish)
                    {
                        tasks.Add(dispatchOperation);
                    }
                    else
                    {
                        var (result, handlersFound) = await dispatchOperation;

                        if (handlersFound)
                        {
                            return result;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);

            // When dispatching a message and no handlers are available, this is a failure.
            if (!publish)
            {
                return new DispatchFailureDispatchResult(dispatchData.MessageType);
            }

            var filteredResult = (await Task.WhenAll(tasks)).Where(p => p.handlersFound).ToList();

            // When publishing a message and no handlers are available, this is a success.
            if (filteredResult.Count == 0)
            {
                return new SuccessDispatchResult();
            }

            if (filteredResult.Count == 1)
            {
                return (await tasks[0]).result;
            }

            return new AggregateDispatchResult(filteredResult.Select(p => p.result));
        }

        #endregion

        #region Typed Dispatcher

        private bool TryGetTypedDispatcher(Type type, out ITypedMessageDispatcher typedDispatcher)
        {
            Assert(type != null);

            var result = _typedDispatchers.TryGetValue(type, out typedDispatcher);

            Assert(!result || typedDispatcher != null);
            Assert(!result || typedDispatcher.MessageType == type);
            return result;
        }

        private TypedMessageDispatcher<TMessage> GetTypedDispatcher<TMessage>()
           where TMessage : class
        {
            return (TypedMessageDispatcher<TMessage>)_typedDispatchers.GetOrAdd(typeof(TMessage), _ => new TypedMessageDispatcher<TMessage>(_serviceProvider));
        }

        private interface ITypedMessageDispatcher
        {
            Task<(IDispatchResult result, bool handlersFound)> DispatchAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation);

            Type MessageType { get; }
        }

        private sealed class TypedMessageDispatcher<TMessage> : ITypedMessageDispatcher
            where TMessage : class
        {
            private readonly HandlerRegistry<IMessageHandler<TMessage>> _registry = new HandlerRegistry<IMessageHandler<TMessage>>();
            private readonly IServiceProvider _serviceProvider;

            public TypedMessageDispatcher(IServiceProvider serviceProvider)
            {
                Assert(serviceProvider != null);
                _serviceProvider = serviceProvider;
            }

            public IHandlerRegistration<IMessageHandler<TMessage>> Register(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
            {
                if (messageHandlerProvider == null)
                    throw new ArgumentNullException(nameof(messageHandlerProvider));

                return _registry.CreateRegistration(messageHandlerProvider);
            }

            public async Task<(IDispatchResult result, bool handlersFound)> DispatchAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation)
            {
                Assert(dispatchData != null);

                // We cannot assume that dispatchData is of type DispatchDataDictionary<TMessage>
                if (!(dispatchData is DispatchDataDictionary<TMessage> typedDispatchData))
                {
                    Assert(dispatchData.Message is TMessage, $"The argument must be of type '{ typeof(TMessage).FullName }' or a derived type.");

                    var message = (TMessage)dispatchData.Message;
                    typedDispatchData = new DispatchDataDictionary<TMessage>(message, dispatchData);
                }

                if (publish)
                {
                    var handlers = _registry.Handlers;

                    if (handlers.Any())
                    {
                        // TODO: Use ValueTaskExtensions.WhenAll
                        var dispatchResults = await Task.WhenAll(handlers.Select(p => DispatchSingleHandlerAsync(p, typedDispatchData, cancellation)).Select(p => p.AsTask()));

                        return (result: new AggregateDispatchResult(dispatchResults), handlersFound: true);
                    }

                    return (result: default, handlersFound: false);
                }
                else
                {
                    if (_registry.TryGetHandler(out var handler))
                    {
                        return (result: await DispatchSingleHandlerAsync(handler, typedDispatchData, cancellation), handlersFound: true);
                    }

                    return (result: default, handlersFound: false);
                }
            }

            private ValueTask<IDispatchResult> DispatchSingleHandlerAsync(IContextualProvider<IMessageHandler<TMessage>> handler,
                                                                          DispatchDataDictionary<TMessage> dispatchData,
                                                                          CancellationToken cancellation)
            {
                Assert(handler != null);
                Assert(dispatchData != null);

                using (var scope = _serviceProvider.CreateScope())
                {
                    try
                    {
                        return handler.ProvideInstance(scope.ServiceProvider).HandleAsync(dispatchData, cancellation);
                    }
                    catch (ConcurrencyException)
                    {
                        return new ValueTask<IDispatchResult>(new ConcurrencyIssueDispatchResult());
                    }
                    catch (Exception exc)
                    {
                        return new ValueTask<IDispatchResult>(new FailureDispatchResult(exc));
                    }
                }
            }

            public Type MessageType => typeof(TMessage);
        }

        #endregion
    }
}
