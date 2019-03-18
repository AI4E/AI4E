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

namespace AI4E
{
    /// <summary>
    /// Defines extensions for the <see cref="IMessageDispatcher"/> and <see cref="INonGenericMessageDispatcher"/> interfaces.
    /// </summary>
    public static class MessageDispatcherExtension
    {
        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message, IEnumerable<KeyValuePair<string, object>> data, CancellationToken cancellation = default)
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

        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message, CancellationToken cancellation = default)
             where TMessage : class
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(message), publish: false, cancellation);
        }

        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, CancellationToken cancellation = default)
             where TMessage : class, new()
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(new TMessage()), publish: false, cancellation);
        }

        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message, IEnumerable<KeyValuePair<string, object>> data, bool publish, CancellationToken cancellation = default)
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

        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message, bool publish, CancellationToken cancellation = default)
             where TMessage : class
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(message), publish, cancellation);
        }

        public static ValueTask<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, bool publish, CancellationToken cancellation = default)
             where TMessage : class, new()
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            return messageDispatcher.DispatchAsync(new DispatchDataDictionary<TMessage>(new TMessage()), publish, cancellation);
        }
    }
}
