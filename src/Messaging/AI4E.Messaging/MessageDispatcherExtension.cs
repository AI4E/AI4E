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
using AI4E.Utils;
using Microsoft.Extensions.Logging;

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extensions for the <see cref="IMessageDispatcher"/> type.
    /// </summary>
    public static class MessageDispatcherExtension
    {
        /// <summary>
        /// Asynchronously performs a query for the specified result data.
        /// </summary>
        /// <typeparam name="TResult">The type of result data.</typeparam>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        public static ValueTask<IDispatchResult> QueryAsync<TResult>(this IMessageDispatcher messageDispatcher, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new Query<TResult>(), cancellation);
        }

        /// <summary>
        /// Asynchronously performs a query for the specified result data by its ID.
        /// </summary>
        /// <typeparam name="TId">The type of result data ID.</typeparam>
        /// <typeparam name="TResult">The type of result data.</typeparam>
        /// <param name="id">The result data ID.</param>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        public static ValueTask<IDispatchResult> QueryByIdAsync<TId, TResult>(this IMessageDispatcher messageDispatcher, TId id, CancellationToken cancellation = default)
            where TId : struct, IEquatable<TId>
        {
            return messageDispatcher.DispatchAsync(new ByIdQuery<TId, TResult>(id), cancellation);
        }

        /// <summary>
        /// Asynchronously performs a query for the specified result data by its ID.
        /// </summary>
        /// <typeparam name="TResult">The type of result data.</typeparam>
        /// <param name="id">The result data ID.</param>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        public static ValueTask<IDispatchResult> QueryByIdAsync<TResult>(this IMessageDispatcher messageDispatcher, Guid id, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new ByIdQuery<TResult>(id), cancellation);
        }

        /// <summary>
        /// Asynchronously performs a query for the specified result data by its parents's ID.
        /// </summary>
        /// <typeparam name="TId">The type of ID.</typeparam>
        /// <typeparam name="TResult">The type of result data.</typeparam>
        /// <param name="parentId">The result data's parent's ID.</param>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        public static ValueTask<IDispatchResult> QueryByParentAsync<TId, TResult>(this IMessageDispatcher messageDispatcher, TId parentId, CancellationToken cancellation = default)
            where TId : struct, IEquatable<TId>
        {
            return messageDispatcher.DispatchAsync(new ByParentQuery<TId, TResult>(parentId), cancellation);
        }

        /// <summary>
        /// Asynchronously performs a query for the specified result data by its parents's ID.
        /// </summary>
        /// <typeparam name="TResult">The type of result data.</typeparam>
        /// <param name="parentId">The result data's parent's ID.</param>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        public static ValueTask<IDispatchResult> QueryByParentAsync<TResult>(this IMessageDispatcher messageDispatcher, Guid parentId, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new ByParentQuery<TResult>(parentId), cancellation);
        }

        private static async void DispatchInternal(
            IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool retryOnFailure,
            ILogger logger)
        {
            // Assuming the argument are already checked.

            try
            {
                IDispatchResult dispatchResult;

                do
                {
                    dispatchResult = await messageDispatcher.DispatchAsync(dispatchData, publish, cancellation: default);
                }
                while (!dispatchResult.IsSuccess && retryOnFailure);
            }
            catch (Exception exc)
            {
                ExceptionHelper.LogException(exc, logger);
            }
        }

        /// <summary>
        /// Dispatches a message of the specified message type and does not wait for a result.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="dispatchData">The dispatch data dictionary.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="retryOnFailure">A boolean value specifying whether the operation shall be retries on failure.</param>
        /// <param name="logger">A logger used to log error messages or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dispatchData"/> is <c>null</c>.</exception>
        public static void Dispatch(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool retryOnFailure = true,
            ILogger logger = null)
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            DispatchInternal(messageDispatcher, dispatchData, publish, retryOnFailure, logger);
        }

        /// <summary>
        /// Dispatches a message of the specified message type and does not wait for a result.
        /// </summary>
        /// <typeparam name="TMessage">The type of message.</typeparam>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="data">Supporting data that shall be dispatched together with the message.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="retryOnFailure">A boolean value specifying whether the operation shall be retries on failure.</param>
        /// <param name="logger">A logger used to log error messages or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="data"/> is <c>null</c>.
        /// </exception>
        public static void Dispatch<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            IEnumerable<KeyValuePair<string, object>> data,
            bool publish = false,
            bool retryOnFailure = true,
            ILogger logger = null)
            where TMessage : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            DispatchInternal(messageDispatcher, new DispatchDataDictionary<TMessage>(message, data), publish, retryOnFailure, logger);
        }

        /// <summary>
        /// Dispatches a message of the specified message type and does not wait for a result.
        /// </summary>
        /// <typeparam name="TMessage">The type of message.</typeparam>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="retryOnFailure">A boolean value specifying whether the operation shall be retries on failure.</param>
        /// <param name="logger">A logger used to log error messages or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="message"/> is <c>null</c>.
        /// </exception>
        public static void Dispatch<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            bool publish = false,
            bool retryOnFailure = true,
            ILogger logger = null)
             where TMessage : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            DispatchInternal(messageDispatcher, new DispatchDataDictionary<TMessage>(message), publish, retryOnFailure, logger);
        }
    }
}
