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

namespace AI4E
{
    public static class MessageDispatcherExtension
    {
        public static Task<IDispatchResult> QueryAsync<TResult>(this IMessageDispatcher messageDispatcher, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new Query<TResult>(), cancellation);
        }

        public static Task<IDispatchResult> QueryByIdAsync<TId, TResult>(this IMessageDispatcher messageDispatcher, TId id, CancellationToken cancellation = default)
            where TId : struct, IEquatable<TId>
        {
            return messageDispatcher.DispatchAsync(new ByIdQuery<TId, TResult>(id), cancellation);
        }

        public static Task<IDispatchResult> QueryByIdAsync<TResult>(this IMessageDispatcher messageDispatcher, Guid id, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new ByIdQuery<TResult>(id), cancellation);
        }

        public static Task<IDispatchResult> QueryByParentAsync<TId, TResult>(this IMessageDispatcher messageDispatcher, TId parentId, CancellationToken cancellation = default)
            where TId : struct, IEquatable<TId>
        {
            return messageDispatcher.DispatchAsync(new ByParentQuery<TId, TResult>(parentId), cancellation);
        }

        public static Task<IDispatchResult> QueryByParentAsync<TResult>(this IMessageDispatcher messageDispatcher, Guid parentId, CancellationToken cancellation = default)
        {
            return messageDispatcher.DispatchAsync(new ByParentQuery<TResult>(parentId), cancellation);
        }
    }
}
