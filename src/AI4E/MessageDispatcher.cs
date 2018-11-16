/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MessageDispatcher.cs
 * Types:           AI4E.MessageDispatcher
 * Version:         1.0
 * Author:          Andreas Trütschel
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
        private readonly ConcurrentDictionary<Type, IHandlerRegistry<IMessageHandler>> _handlers;

        #endregion

        #region C'tor

        public MessageDispatcher(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _handlers = new ConcurrentDictionary<Type, IHandlerRegistry<IMessageHandler>>();
            _serviceProvider = serviceProvider;
        }

        #endregion

        #region IMessageDispatcher

        public IHandlerRegistration Register(Type messageType, IContextualProvider<IMessageHandler> messageHandlerProvider)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            return GetHandlerRegistry(messageType).CreateRegistration(messageHandlerProvider);
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

                if (TryGetHandlerRegistry(currType, out var handlerRegistry))
                {
                    var dispatchOperation = DispatchAsync(handlerRegistry, dispatchData, publish, cancellation);

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

        public async Task<(IDispatchResult result, bool handlersFound)> DispatchAsync(IHandlerRegistry<IMessageHandler> handlerRegistry, DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation)
        {
            Assert(dispatchData != null);

            if (publish)
            {
                var handlers = handlerRegistry.Handlers;

                if (handlers.Any())
                {
                    // TODO: Use ValueTaskExtensions.WhenAll
                    var dispatchResults = await Task.WhenAll(handlers.Select(p => DispatchSingleHandlerAsync(p, dispatchData, cancellation)).Select(p => p.AsTask()));

                    return (result: new AggregateDispatchResult(dispatchResults), handlersFound: true);
                }

                return (result: default, handlersFound: false);
            }
            else
            {
                if (handlerRegistry.TryGetHandler(out var handler))
                {
                    return (result: await DispatchSingleHandlerAsync(handler, dispatchData, cancellation), handlersFound: true);
                }

                return (result: default, handlersFound: false);
            }
        }

        private ValueTask<IDispatchResult> DispatchSingleHandlerAsync(IContextualProvider<IMessageHandler> handler,
                                                                      DispatchDataDictionary dispatchData,
                                                                      CancellationToken cancellation)
        {
            Assert(handler != null);
            Assert(dispatchData != null);

            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    var handlerInstance = handler.ProvideInstance(scope.ServiceProvider);

                    if (!handlerInstance.MessageType.IsAssignableFrom(dispatchData.MessageType))
                    {
                        throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{handlerInstance.MessageType}'.");
                    }

                    return handlerInstance.HandleAsync(dispatchData, cancellation);
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

        #endregion

        private bool TryGetHandlerRegistry(Type messageType, out IHandlerRegistry<IMessageHandler> handlerRegistry)
        {
            Assert(messageType != null);
            if (_handlers.TryGetValue(messageType, out var handlers))
            {
                handlerRegistry = handlers;
                return true;
            }

            handlerRegistry = default;
            return false;
        }

        private IHandlerRegistry<IMessageHandler> GetHandlerRegistry(Type messageType)
        {
            Assert(messageType != null);
            return _handlers.GetOrAdd(messageType, _ => new HandlerRegistry<IMessageHandler>());
        }
    }
}
