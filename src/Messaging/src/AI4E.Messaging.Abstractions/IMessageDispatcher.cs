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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;

namespace AI4E.Messaging
{
    /// <summary>
    /// Represents a message dispatcher that dispatches messages to message handlers.
    /// </summary>
    public interface IMessageDispatcher : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Gets the <see cref="IMessageHandlerProvider"/> that is used to load (local) message handlers.
        /// </summary>
        IMessageHandlerProvider MessageHandlerProvider { get; }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="dispatchData">
        /// The dispatch data dictionary that contains the message and supporting values.
        /// </param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation = default)
        {
            return DispatchAsync(dispatchData, publish, RouteEndPointScope.NoScope, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type to the specified end-point address.
        /// </summary>
        /// <param name="dispatchData">
        /// The dispatch data dictionary that contains the message and supporting values.
        /// </param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="endPoint">The end-point of the receiver, the message shall be dispatched to.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IDispatchResult}"/> representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            RouteEndPointAddress endPoint,
            CancellationToken cancellation = default)
        {
            return DispatchAsync(dispatchData, publish, new RouteEndPointScope(endPoint), cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type to the specified end-point scope.
        /// </summary>
        /// <param name="dispatchData">
        /// The dispatch data dictionary that contains the message and supporting values.
        /// </param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="remoteScope">The end-point scope of the receiver, the message shall be dispatched to.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IDispatchResult}"/> representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            RouteEndPointScope remoteScope,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type to the local end-point.
        /// </summary>
        /// <param name="dispatchData">
        /// The dispatch data dictionary that contains the message and supporting values.
        /// </param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IDispatchResult}"/> representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// It is not guaranteed that the message is dispatched to the current message dispatcher, if the are other
        /// message dispatchers with the same local end-point present in the routing system. This method is a shortcut
        /// for dispatching to the end-point, as retrieved from <see cref="GetLocalEndPointAsync(CancellationToken)"/> 
        /// and to the same scope as the current message-dispatcher.
        /// </remarks>
        async ValueTask<IDispatchResult> DispatchLocalAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation = default)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
            var result = await DispatchAsync(
                dispatchData, publish, new RouteEndPointScope(localEndPoint), cancellation).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Asynchronously retrieves the local end-point scope of the message dispatcher.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{RouteEndPointScope}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the local end-point scope of the message dispatcher.
        /// </returns>
        ValueTask<RouteEndPointScope> GetScopeAsync(CancellationToken cancellation = default);

        // TODO: Rename to GetLocalScopeAsync

        /// <summary>
        /// Asynchronously retrieves the local end-point of the message dispatcher.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{RouteEndPointAddress}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the local end-point of the message dispatcher.
        /// </returns>
        async ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default)
        {
            var scope = await GetScopeAsync(cancellation).ConfigureAwait(false);
            return scope.EndPointAddress;
        }
    }
}
