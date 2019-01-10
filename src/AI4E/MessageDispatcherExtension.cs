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
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using Microsoft.Extensions.Logging;

namespace AI4E
{
    public static class MessageDispatcherExtension
    {
        public static ValueTask<IDispatchResult> QueryAsync<TResult>(this IMessageDispatcher messageDispatcher, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new Query<TResult>(), cancellation);
        }

        public static ValueTask<IDispatchResult> QueryByIdAsync<TId, TResult>(this IMessageDispatcher messageDispatcher, TId id, CancellationToken cancellation = default)
            where TId : struct, IEquatable<TId>
        {
            return messageDispatcher.DispatchAsync(new ByIdQuery<TId, TResult>(id), cancellation);
        }

        public static ValueTask<IDispatchResult> QueryByIdAsync<TResult>(this IMessageDispatcher messageDispatcher, Guid id, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new ByIdQuery<TResult>(id), cancellation);
        }

        public static ValueTask<IDispatchResult> QueryByParentAsync<TId, TResult>(this IMessageDispatcher messageDispatcher, TId parentId, CancellationToken cancellation = default)
            where TId : struct, IEquatable<TId>
        {
            return messageDispatcher.DispatchAsync(new ByParentQuery<TId, TResult>(parentId), cancellation);
        }

        public static ValueTask<IDispatchResult> QueryByParentAsync<TResult>(this IMessageDispatcher messageDispatcher, Guid parentId, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new ByParentQuery<TResult>(parentId), cancellation);
        }

        // Do NOT wait for the messages to be dispatched (fire and forget)
        public static async void Dispatch(this IMessageDispatcher messageDispatcher, DispatchDataDictionary dispatchData, bool publish, bool retryOnFailure = true, ILogger logger = null)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));
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

        // Do NOT wait for the messages to be dispatched (fire and forget)
        public static void Dispatch<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message, IEnumerable<KeyValuePair<string, object>> data, bool publish = false, bool retryOnFailure = true, ILogger logger = null)
            where TMessage : class
        {
            Dispatch(messageDispatcher, new DispatchDataDictionary<TMessage>(message, data), publish, retryOnFailure, logger);
        }

        // Do NOT wait for the messages to be dispatched (fire and forget)
        public static void Dispatch<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message, bool publish = false, bool retryOnFailure = true, ILogger logger = null)
             where TMessage : class
        {
            Dispatch(messageDispatcher, message, ImmutableDictionary<string, object>.Empty, publish, retryOnFailure, logger);
        }
    }
}
