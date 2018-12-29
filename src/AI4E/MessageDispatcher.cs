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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E
{
    public sealed class MessageDispatcher : IMessageDispatcher
    {
        #region Fields

        private readonly IMessageHandlerRegistry _messageHandlerRegistry;
        private readonly IServiceProvider _serviceProvider;
        private volatile IMessageHandlerProvider _messageHandlerProvider;

        #endregion

        #region C'tor

        public MessageDispatcher(IMessageHandlerRegistry messageHandlerRegistry, IServiceProvider serviceProvider)
        {
            if (messageHandlerRegistry == null)
                throw new ArgumentNullException(nameof(messageHandlerRegistry));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _messageHandlerRegistry = messageHandlerRegistry;
            _serviceProvider = serviceProvider;
        }

        #endregion

        // TODO: Do we allow handler reload? How can consistency be guaranteed for the remote routing system?
        private ValueTask ReloadHandlersAsync()
        {
            _messageHandlerProvider = null; // Volatile write op.

            return default;
        }

        #region IMessageDispatcher

        public async Task<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            return (await TryDispatchAsync(dispatchData, publish, allowRouteDescend: true, cancellation)).result;
        }

        #endregion

        public IMessageHandlerProvider MessageHandlerProvider => GetMessageHandlerProvider();

        private IMessageHandlerProvider GetMessageHandlerProvider()
        {
            var messageHandlerProvider = _messageHandlerProvider; // Volatile read op.

            if (messageHandlerProvider == null)
            {
                messageHandlerProvider = _messageHandlerRegistry.ToProvider();
                var previous = Interlocked.CompareExchange(ref _messageHandlerProvider, messageHandlerProvider, null);

                if (previous != null)
                {
                    messageHandlerProvider = previous;
                }
            }

            Assert(messageHandlerProvider != null);

            return messageHandlerProvider;
        }

        public async ValueTask<(IDispatchResult result, bool handlersFound)> TryDispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool allowRouteDescend,
            CancellationToken cancellation)

        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            var messageHandlerProvider = GetMessageHandlerProvider();

            var currType = dispatchData.MessageType;
            var tasks = new List<Task<(IDispatchResult result, bool handlersFound)>>();

            do
            {
                Assert(currType != null);

                var handlerCollection = messageHandlerProvider.GetHandlers(currType);

                if (handlerCollection.Any())
                {
                    var dispatchOperation = DispatchAsync(handlerCollection, dispatchData, publish, cancellation);

                    if (publish)
                    {
                        tasks.Add(dispatchOperation);
                    }
                    else
                    {
                        var (result, handlersFound) = await dispatchOperation;

                        if (handlersFound)
                        {
                            return (result, handlersFound: true);
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }
            while (allowRouteDescend && !currType.IsInterface && (currType = currType.BaseType) != null);

            // When dispatching a message and no handlers are available, this is a failure.
            if (!publish)
            {
                return (new DispatchFailureDispatchResult(dispatchData.MessageType), handlersFound: false);
            }

            var filteredResult = (await Task.WhenAll(tasks)).Where(p => p.handlersFound).ToList();

            // When publishing a message and no handlers are available, this is a success.
            if (filteredResult.Count == 0)
            {
                return (new SuccessDispatchResult(), handlersFound: false);
            }

            if (filteredResult.Count == 1)
            {
                return ((await tasks[0]).result, handlersFound: true);
            }

            return (new AggregateDispatchResult(filteredResult.Select(p => p.result)), handlersFound: true);
        }


        private async Task<(IDispatchResult result, bool handlersFound)> DispatchAsync(
            IEnumerable<IMessageHandlerFactory> handlerCollection,
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            Assert(dispatchData != null);
            Assert(handlerCollection != null);
            Assert(handlerCollection.Any());

            if (publish)
            {
                var dispatchResults = await ValueTaskHelper.WhenAll(handlerCollection.Select(p => DispatchSingleHandlerAsync(p, dispatchData, publish, cancellation)));

                return (result: new AggregateDispatchResult(dispatchResults), handlersFound: true);
            }
            else
            {
                foreach (var handlerFactory in handlerCollection)
                {
                    var result = await DispatchSingleHandlerAsync(handlerFactory, dispatchData, publish, cancellation);

                    if (result.IsDispatchFailure())
                    {
                        continue;
                    }

                    return (result, handlersFound: true);
                }

                return (result: default, handlersFound: false);
            }
        }

        private async ValueTask<IDispatchResult> DispatchSingleHandlerAsync(
            IMessageHandlerFactory handlerFactory,
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            Assert(handlerFactory != null);
            Assert(dispatchData != null);

            using (var scope = _serviceProvider.CreateScope())
            {
                try
                {
                    var handlerInstance = handlerFactory.CreateMessageHandler(scope.ServiceProvider);

                    if (handlerInstance == null)
                    {
                        throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that is null.");
                    }

                    if (!handlerInstance.MessageType.IsAssignableFrom(dispatchData.MessageType))
                    {
                        throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{handlerInstance.MessageType}'.");
                    }

                    return await handlerInstance.HandleAsync(dispatchData, publish, cancellation);
                }
                catch (ConcurrencyException)
                {
                    return new ConcurrencyIssueDispatchResult();
                }
                catch (Exception exc)
                {
                    return new FailureDispatchResult(exc);
                }
            }
        }
    }
}
