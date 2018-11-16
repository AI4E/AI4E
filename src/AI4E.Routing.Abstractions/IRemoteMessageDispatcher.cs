/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IRemoteMessageDispatcher.cs 
 * Types:           AI4E.Routing.IRemoteMessageDispatcher
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Routing
{
    public interface IRemoteMessageDispatcher : IMessageDispatcher
    {
        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageType">The type of message.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="receiver">The reveiver, the message shall be dispatched to.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="messageType"/> or <paramref name="message"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="message"/> is not of type <paramref name="messageType"/> or a derived type.</exception>
        Task<IDispatchResult> DispatchAsync(DispatchDataDictionary dispatchData, bool publish, EndPointAddress endPoint, CancellationToken cancellation = default); // TODO: Return ValueTask<IDispatchResult>

        Task<IDispatchResult> DispatchLocalAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation = default); // TODO: Return ValueTask<IDispatchResult>

        ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Registers a message handler.
        /// </summary>
        /// <param name="messageType">The type of message.</param>
        /// <param name="messageHandlerProvider">The message handler provider to register.</param>
        /// <param name="options">The options used for handler registration. </param>
        /// <returns>
        /// A <see cref="IHandlerRegistration"/> that represents the handlers registration.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="messageType"/> or <paramref name="messageHandlerProvider"/> is null.</exception>
        IHandlerRegistration Register(Type messageType, IContextualProvider<IMessageHandler> messageHandlerProvider, RouteRegistrationOptions options);
    }

    public static class RemoteMessageDispatcherExtension
    {
        public static IHandlerRegistration<IMessageHandler<TMessage>> Register<TMessage>(
            this IRemoteMessageDispatcher messageDispatcher,
            IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider,
            RouteRegistrationOptions options)
            where TMessage : class
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            var messageType = typeof(TMessage);
            var handlerRegistration = messageDispatcher.Register(typeof(TMessage), new TypedMessageHandlerProvider<TMessage>(messageHandlerProvider), options);

            return new TypedHandleRegistration<TMessage>(messageHandlerProvider, handlerRegistration);
        }
    }
}
