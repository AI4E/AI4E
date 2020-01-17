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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;

namespace AI4E.Messaging
{
    /// <summary>
    /// Implements the null-object design pattern for the <see cref="IMessageDispatcher"/> interface.
    /// </summary>
    public sealed class NoMessageDispatcher : IMessageDispatcher
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="NoMessageDispatcher"/> type.
        /// </summary>
        public static NoMessageDispatcher Instance { get; } = new NoMessageDispatcher();

        private NoMessageDispatcher() { }

        IMessageHandlerProvider IMessageDispatcher.MessageHandlerProvider => MessageHandlerProvider;

        /// <summary>
        /// Gets the message handler provider, 
        /// which is always the singleton instance of <see cref="NoMessageHandlerProvider"/>.
        /// </summary>
        public NoMessageHandlerProvider MessageHandlerProvider => NoMessageHandlerProvider.Instance;

        /// <inheritdoc />
        public ValueTask<IDispatchResult> DispatchAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation = default)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            return new ValueTask<IDispatchResult>(new DispatchFailureDispatchResult(dispatchData.MessageType));
        }

        /// <inheritdoc />
        public ValueTask<IDispatchResult> DispatchAsync(DispatchDataDictionary dispatchData, bool publish, RouteEndPointAddress endPoint, CancellationToken cancellation = default)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            return new ValueTask<IDispatchResult>(new DispatchFailureDispatchResult(dispatchData.MessageType));
        }

        /// <inheritdoc />
        public ValueTask<IDispatchResult> DispatchLocalAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation = default)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            return new ValueTask<IDispatchResult>(new DispatchFailureDispatchResult(dispatchData.MessageType));
        }

        /// <summary>
        /// Asynchronously retrieves the local end-point address, which is always the default value of <see cref="RouteEndPointAddress"/>.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> to cancel the asynchronous operation,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the local end-point address of the message dispatcher,
        /// which is always the default value of <see cref="RouteEndPointAddress"/>.
        /// </returns>
        public ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default)
        {
            return new ValueTask<RouteEndPointAddress>(result: default);
        }

        /// <inheritdoc />
        public void Dispose() { }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
