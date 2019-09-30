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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extensions for <see cref="IMessageDispatcher"/>s.
    /// </summary>
    public static class MessageDispatcherExtension
    {
        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="data">A collection of key value pairs that contain additional dispatch data.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <typeparam name="TMessage">The type of message tp dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="messageDispatcher"/>, <paramref name="message"/> or <paramref name="data"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            IEnumerable<KeyValuePair<string, object>> data,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(message, data), publish: false, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <typeparam name="TMessage">The type of message tp dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(message), publish: false, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <typeparam name="TMessage">The type of message tp dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.</exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            CancellationToken cancellation = default)
            where TMessage : class, new()
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(new TMessage()), publish: false, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="data">A collection of key value pairs that contain additional dispatch data.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <typeparam name="TMessage">The type of message tp dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="messageDispatcher"/>, <paramref name="message"/> or <paramref name="data"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            IEnumerable<KeyValuePair<string, object>> data,
            bool publish,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(message, data), publish, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <typeparam name="TMessage">The type of message tp dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            bool publish,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(message), publish, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <typeparam name="TMessage">The type of message tp dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.</exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            bool publish,
            CancellationToken cancellation = default)
            where TMessage : class, new()
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(new TMessage()), publish, cancellation);
        }
    }
}
