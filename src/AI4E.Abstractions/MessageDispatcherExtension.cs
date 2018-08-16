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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E
{
    /// <summary>
    /// Defines extensions for the <see cref="IMessageDispatcher"/> and <see cref="INonGenericMessageDispatcher"/> interfaces.
    /// </summary>
    public static class MessageDispatcherExtension
    {
        public static Task<IDispatchResult> PublishAsync<TMessage>(this IMessageDispatcher messageDispatcher,
                                                                   TMessage message,
                                                                   DispatchValueDictionary context,
                                                                   CancellationToken cancellation = default)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(message, context, publish: true, cancellation);
        }

        public static Task<IDispatchResult> PublishAsync<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message, CancellationToken cancellation = default)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(message, new DispatchValueDictionary(), publish: true, cancellation);
        }

        public static Task<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message, CancellationToken cancellation = default)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(message, publish: false, cancellation);
        }

        public static Task<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, CancellationToken cancellation = default)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            var message = default(TMessage);

            try
            {
                message = Activator.CreateInstance<TMessage>();
            }
            catch (MissingMethodException exc)
            {
                throw new ArgumentException("The specified message must have a parameterless constructor.", exc);
            }

            Debug.Assert(message != null);

            return messageDispatcher.DispatchAsync(message, cancellation);
        }

        public static Task<IDispatchResult> DispatchAsync(this IMessageDispatcher messageDispatcher, object message, bool publish, CancellationToken cancellation = default)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(message.GetType(), message, new DispatchValueDictionary(), publish, cancellation);
        }

        public static Task<IDispatchResult> DispatchAsync(this IMessageDispatcher messageDispatcher, object message, CancellationToken cancellation = default)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(message.GetType(), message, new DispatchValueDictionary(), publish: false, cancellation);
        }

        public static Task<IDispatchResult> DispatchAsync(this IMessageDispatcher messageDispatcher, Type messageType, CancellationToken cancellation = default)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            var message = default(object);

            try
            {
                message = Activator.CreateInstance(messageType);
            }
            catch (MissingMethodException exc)
            {
                throw new ArgumentException("The specified message must have a parameterless constructor.", exc);
            }

            Debug.Assert(message != null);

            return messageDispatcher.DispatchAsync(message, cancellation);
        }
    }
}
