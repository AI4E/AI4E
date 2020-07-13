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

namespace AI4E.Messaging
{
    /// <summary>
    /// Represents a message handler.
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// Asynchronously handles the specified message.
        /// </summary>
        /// <param name="dispatchData">The dispatch data that contains the message to handle and supporting data.</param>
        /// <param name="publish">A boolean value specifying whether the message is published to all handlers.</param>
        /// <param name="localDispatch">A boolean value specifying whether the message is dispatched locally.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dispatchData"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the type of the specified message is not assignable to the handlers message type.</exception>
        ValueTask<IDispatchResult> HandleAsync(DispatchDataDictionary dispatchData, bool publish, bool localDispatch, CancellationToken cancellation);

        /// <summary>
        /// Gets the message type, the handler can handle.
        /// </summary>
        Type MessageType { get; }
    }

    /// <summary>
    /// Represents a message handler that can handle messages of the specified type.
    /// </summary>
    /// <typeparam name="TMessage">The type of message.</typeparam>
    public interface IMessageHandler<TMessage> : IMessageHandler
        where TMessage : class
    {
        /// <summary>
        /// Asynchronously handles the specified message.
        /// </summary>
        /// <param name="dispatchData">The dispatch data that contains the message to handle and supporting data.</param>
        /// <param name="publish">A boolean value specifying whether the message is published to all handlers.</param>
        /// <param name="localDispatch">A boolean value specifying whether the message is dispatched locally.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dispatchData"/> is null.</exception>
        ValueTask<IDispatchResult> HandleAsync(DispatchDataDictionary<TMessage> dispatchData, bool publish, bool localDispatch, CancellationToken cancellation);

        ValueTask<IDispatchResult> IMessageHandler.HandleAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            bool localDispatch,
            CancellationToken cancellation)
        {
            return HandleAsync(dispatchData.As<TMessage>(), publish, localDispatch, cancellation);
        }

        Type IMessageHandler.MessageType => typeof(TMessage);
    }
}
