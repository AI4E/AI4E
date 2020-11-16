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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils;
using Microsoft.Extensions.Logging;

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extensions for <see cref="IMessageDispatcher"/>s.
    /// </summary>
    public static class MessageDispatcherExtension
    {
        // These extension methods are here for representation of default method implementations in IMessageDispatcher,
        // as callers cannot bind to them when the type is not casted to IMessageDispatcher

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
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
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation = default)
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(dispatchData, publish, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
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
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            CancellationToken cancellation = default) where TMessage : class
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(dispatchData, publish, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type to the specified end-point address.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
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
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchData,
            bool publish,
            RouteEndPointAddress endPoint,
            CancellationToken cancellation = default)
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(dispatchData, publish, endPoint, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type to the specified end-point address.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
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
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
        /// <returns>
        /// A <see cref="ValueTask{IDispatchResult}"/> representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            RouteEndPointAddress endPoint,
            CancellationToken cancellation = default) where TMessage : class
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(dispatchData, publish, endPoint, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type to the local end-point.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
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
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// It is not guaranteed that the message is dispatched to the current message dispatcher, if the are other
        /// message dispatchers with the same local end-point present in the routing system. This method is a shortcut
        /// for dispatching to the end-point, as retrieved from <see cref="GetLocalEndPointAsync(CancellationToken)"/> 
        /// and to the same scope as the current message-dispatcher. 
        /// The scope of a non-scoped message dispatcher is <see cref="RouteEndPointScope.NoScope"/>.
        /// </remarks>
        public static ValueTask<IDispatchResult> DispatchLocalAsync(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation = default)
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchLocalAsync(dispatchData, publish, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type to the local end-point.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
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
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
        /// <returns>
        /// A <see cref="ValueTask{IDispatchResult}"/> representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// It is not guaranteed that the message is dispatched to the current message dispatcher, if the are other
        /// message dispatchers with the same local end-point present in the routing system. This method is a shortcut
        /// for dispatching to the end-point, as retrieved from <see cref="GetLocalEndPointAsync(CancellationToken)"/> 
        /// and to the same scope as the current message-dispatcher. 
        /// The scope of a non-scoped message dispatcher is <see cref="RouteEndPointScope.NoScope"/>.
        /// </remarks>
        public static ValueTask<IDispatchResult> DispatchLocalAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            CancellationToken cancellation = default) where TMessage : class
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchLocalAsync(dispatchData, publish, cancellation);
        }


        public static ValueTask<IDispatchResult> DispatchAsync(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchDataDictionary,
            CancellationToken cancellation = default)
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(dispatchDataDictionary, publish: false, cancellation);
        }

        // To prevent the compiler to bind to the wrong extension method.
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary<TMessage> dispatchDataDictionary,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(dispatchDataDictionary, publish: false, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="data">A collection of key value pairs that contain additional dispatch data.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="messageDispatcher"/>, <paramref name="message"/> 
        /// or <paramref name="data"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            IEnumerable<KeyValuePair<string, object?>> data,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (data is null)
                throw new ArgumentNullException(nameof(data));

#pragma warning disable CA1062
            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(message, data), publish: false, cancellation);
#pragma warning restore CA1062
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
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
            if (message is null)
                throw new ArgumentNullException(nameof(message));

#pragma warning disable CA1062
            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(message), publish: false, cancellation);
#pragma warning restore CA1062
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            CancellationToken cancellation = default)
            where TMessage : class, new()
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(
                new DispatchDataDictionary<TMessage>(new TMessage()), publish: false, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="data">A collection of key value pairs that contain additional dispatch data.</param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="messageDispatcher"/>, <paramref name="message"/> 
        /// or <paramref name="data"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            IEnumerable<KeyValuePair<string, object?>> data,
            bool publish,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (data is null)
                throw new ArgumentNullException(nameof(data));

            return messageDispatcher.DispatchAsync(
                new DispatchDataDictionary<TMessage>(message, data), publish, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
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
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message is null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(
                new DispatchDataDictionary<TMessage>(message), publish, cancellation);
        }

        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <typeparam name="TMessage">The type of message to dispatch.</typeparam>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            bool publish,
            CancellationToken cancellation = default)
            where TMessage : class, new()
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(
                new DispatchDataDictionary<TMessage>(new TMessage()), publish, cancellation);
        }

        /// <summary>
        /// Asynchronously performs a query for the specified result data.
        /// </summary>
        /// <typeparam name="TResult">The type of result data.</typeparam>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> QueryAsync<TResult>(
            this IMessageDispatcher messageDispatcher, CancellationToken cancellation = default)
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
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> QueryByIdAsync<TId, TResult>(
            this IMessageDispatcher messageDispatcher,
            TId id,
            CancellationToken cancellation = default)
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
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> QueryByIdAsync<TResult>(
            this IMessageDispatcher messageDispatcher,
            Guid id,
            CancellationToken cancellation = default)
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
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> QueryByParentAsync<TId, TResult>(
            this IMessageDispatcher messageDispatcher,
            TId parentId,
            CancellationToken cancellation = default)
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
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageDispatcher"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IDispatchResult> QueryByParentAsync<TResult>(
            this IMessageDispatcher messageDispatcher,
            Guid parentId,
            CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new ByParentQuery<TResult>(parentId), cancellation);
        }

#pragma warning disable VSTHRD100
        private static async void DispatchInternal(
#pragma warning restore VSTHRD100
            IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool retryOnFailure,
            ILogger? logger)
        {
            // Assuming the arguments are already checked.

            try
            {
                IDispatchResult dispatchResult;

                do
                {
                    dispatchResult = await messageDispatcher.DispatchAsync(
                        dispatchData, publish, cancellation: default).ConfigureAwait(false);
                }
                while (!dispatchResult.IsSuccess && retryOnFailure);
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                ExceptionHelper.LogException(exc, logger);
            }
        }

        /// <summary>
        /// Dispatches a message of the specified message type and does not wait for a result.
        /// </summary>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="dispatchData">The dispatch data dictionary.</param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="retryOnFailure">
        /// A boolean value specifying whether the operation shall be retries on failure.
        /// </param>
        /// <param name="logger">A logger used to log error messages or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messageDispatcher"/> or <paramref name="dispatchData"/> is <c>null</c>.
        /// </exception>
        public static void Dispatch(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool retryOnFailure = true,
            ILogger? logger = null)
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (dispatchData is null)
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
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="retryOnFailure">
        /// A boolean value specifying whether the operation shall be retries on failure.
        /// </param>
        /// <param name="logger">A logger used to log error messages or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="messageDispatcher"/>,  <paramref name="message"/> 
        /// or <paramref name="data"/> is <c>null</c>.
        /// </exception>
        public static void Dispatch<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            IEnumerable<KeyValuePair<string, object?>> data,
            bool publish = false,
            bool retryOnFailure = true,
            ILogger? logger = null)
            where TMessage : class
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (data is null)
                throw new ArgumentNullException(nameof(data));

            DispatchInternal(
                messageDispatcher,
                new DispatchDataDictionary<TMessage>(message, data), publish, retryOnFailure, logger);
        }

        /// <summary>
        /// Dispatches a message of the specified message type and does not wait for a result.
        /// </summary>
        /// <typeparam name="TMessage">The type of message.</typeparam>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to dispatch.</param>
        /// <param name="publish">
        /// A boolean value specifying whether the message shall be published to all handlers.
        /// </param>
        /// <param name="retryOnFailure">
        /// A boolean value specifying whether the operation shall be retries on failure.
        /// </param>
        /// <param name="logger">A logger used to log error messages or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageDispatcher"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        public static void Dispatch<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            bool publish = false,
            bool retryOnFailure = true,
            ILogger? logger = null)
             where TMessage : class
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message is null)
                throw new ArgumentNullException(nameof(message));

            DispatchInternal(
                messageDispatcher,
                new DispatchDataDictionary<TMessage>(message), publish, retryOnFailure, logger);
        }
    }
}
