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
    /// <summary>
    /// Represents a message dispatcher that dispatches messages to message handlers.
    /// </summary>
    public sealed class MessageDispatcher : IMessageDispatcher
    {
        #region Fields

        private readonly IMessageHandlerRegistry _messageHandlerRegistry;
        private readonly IServiceProvider _serviceProvider;
        private volatile IMessageHandlerProvider _messageHandlerProvider;

        #endregion

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="MessageDispatcher"/> type.
        /// </summary>
        /// <param name="messageHandlerRegistry">The <see cref="IMessageHandlerProvider"/> that is used to load message handlers.</param>
        /// <param name="serviceProvider">The service provider that is used to obtain services.</param>
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

        /// <inheritdoc />
        public async ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
#pragma warning disable CS0612
            return (await TryDispatchAsync(dispatchData, publish, localDispatch: true, allowRouteDescend: true, cancellation: cancellation)).result;
#pragma warning restore CS0612
        }

        #endregion

        /// <summary>
        /// Gets the <see cref="IMessageHandlerProvider"/> that is used to load message handlers.
        /// </summary>
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

        /// <summary>
        /// Do NOT use this directly. This will become an internal API.
        /// </summary>
        [Obsolete] // TODO: This should be an internal API
        public async ValueTask<(IDispatchResult result, bool handlersFound)> TryDispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            bool allowRouteDescend,
            CancellationToken cancellation)

        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            var messageHandlerProvider = GetMessageHandlerProvider();

            var currType = dispatchData.MessageType;
            var tasks = new List<ValueTask<(IDispatchResult result, bool handlersFound)>>();

            do
            {
                Assert(currType != null);

                var handlerRegistrations = messageHandlerProvider.GetHandlerRegistrations(currType);

                if (handlerRegistrations.Any())
                {
                    var dispatchOperation = DispatchAsync(handlerRegistrations, dispatchData, publish, localDispatch, cancellation);

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

            var filteredResult = (await tasks.WhenAll(preserveOrder: false))
                .Where(p => p.handlersFound)
                .Select(p => p.result)
                .ToList();

            // When publishing a message and no handlers are available, this is a success.
            if (filteredResult.Count == 0)
            {
                return (new SuccessDispatchResult(), handlersFound: false);
            }

            if (filteredResult.Count == 1)
            {
                return ((await tasks[0]).result, handlersFound: true);
            }

            return (new AggregateDispatchResult(filteredResult), handlersFound: true);
        }


        private async ValueTask<(IDispatchResult result, bool handlersFound)> DispatchAsync(
            IReadOnlyCollection<IMessageHandlerRegistration> handlerRegistrations,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Assert(dispatchData != null);
            Assert(handlerRegistrations != null);
            Assert(handlerRegistrations.Any());

            if (publish)
            {
                var dispatchOperations = new List<ValueTask<IDispatchResult>>(capacity: handlerRegistrations.Count);

                foreach (var handlerRegistration in handlerRegistrations)
                {
                    if (!localDispatch && handlerRegistration.IsLocalDispatchOnly())
                    {
                        continue;
                    }

                    var dispatchOperation = DispatchSingleHandlerAsync(
                        handlerRegistration, dispatchData, publish, localDispatch, cancellation);

                    dispatchOperations.Add(dispatchOperation);
                }

                if (!dispatchOperations.Any())
                {
                    return (result: new SuccessDispatchResult(), handlersFound: false);
                }

                var dispatchResults = await dispatchOperations.WhenAll(preserveOrder: false);

                if (dispatchResults.Count() == 1)
                {
                    return (result: dispatchResults.First(), handlersFound: true);
                }

                return (result: new AggregateDispatchResult(dispatchResults), handlersFound: true);
            }
            else
            {
                foreach (var handlerRegistration in handlerRegistrations)
                {
                    if (handlerRegistration.IsPublishOnly())
                    {
                        continue;
                    }

                    if (!localDispatch && handlerRegistration.IsLocalDispatchOnly())
                    {
                        continue;
                    }

                    var result = await DispatchSingleHandlerAsync(
                        handlerRegistration, dispatchData, publish, localDispatch, cancellation);

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
            IMessageHandlerRegistration handlerRegistration,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            Assert(handlerRegistration != null);
            Assert(dispatchData != null);

            using var scope = _serviceProvider.CreateScope();
            var handler = handlerRegistration.CreateMessageHandler(scope.ServiceProvider);

            if (handler == null)
            {
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that is null.");
            }

            if (!handler.MessageType.IsAssignableFrom(dispatchData.MessageType))
            {
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{handler.MessageType}'.");
            }

            try
            {
                return await handler.HandleAsync(dispatchData, publish, localDispatch, cancellation);
            }
            catch (Exception exc)
            {
                return new FailureDispatchResult(exc);
            }
        }
    }
}
