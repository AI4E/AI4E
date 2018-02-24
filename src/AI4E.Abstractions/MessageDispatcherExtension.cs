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
using System.Threading.Tasks;

namespace AI4E
{
    /// <summary>
    /// Defines extensions for the <see cref="IMessageDispatcher"/> and <see cref="INonGenericMessageDispatcher"/> interfaces.
    /// </summary>
    public static class MessageDispatcherExtension
    {
        public static async Task<TResult> DispatchAsync<TMessage, TResult>(this IMessageDispatcher messageDispatcher, TMessage message)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var dispatchResult = await messageDispatcher.DispatchAsync(message);

            if (dispatchResult.IsSuccess && dispatchResult is IDispatchResult<TResult> typedDispatchResult)
            {
                return typedDispatchResult.Result;
            }

            return default;
        }

        public static Task<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher, TMessage message)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(message, publish: false);
        }

        public static Task<IDispatchResult> DispatchAsync<TMessage>(this IMessageDispatcher messageDispatcher)
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

            return messageDispatcher.DispatchAsync(message);
        }

        public static Task<TResult> DispatchAsync<TMessage, TResult>(this IMessageDispatcher messageDispatcher)
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

            return messageDispatcher.DispatchAsync<TMessage, TResult>(message);
        }

        public static Task<IDispatchResult> DispatchAsync(this IMessageDispatcher messageDispatcher, object message, bool publish)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(message.GetType(), message, new DispatchValueDictionary(), publish);
        }

        public static Task<IDispatchResult> DispatchAsync(this IMessageDispatcher messageDispatcher, object message)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return messageDispatcher.DispatchAsync(message.GetType(), message, new DispatchValueDictionary(), publish: false);
        }

        public static async Task<object> DispatchAsync(this IMessageDispatcher messageDispatcher, object message, Type resultType)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (resultType == null)
                throw new ArgumentNullException(nameof(resultType));

            var dispatchResult = await messageDispatcher.DispatchAsync(message);

            if (dispatchResult.IsSuccess)
            {
                var dispatchResultType = dispatchResult.GetType();

                foreach (var iface in dispatchResultType.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDispatchResult<>))
                    {
                        var actualResultType = iface.GetGenericArguments()[0];

                        if (resultType.IsAssignableFrom(actualResultType))
                        {
                            return typeof(IDispatchResult<>).MakeGenericType(actualResultType)
                                                            .GetProperty("Result")
                                                            .GetValue(dispatchResult);
                        }
                    }
                }
            }

            return null;
        }

        public static Task<object> DispatchAsync(this IMessageDispatcher messageDispatcher, Type messageType, Type resultType)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (resultType == null)
                throw new ArgumentNullException(nameof(resultType));

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

            return messageDispatcher.DispatchAsync(message, resultType);
        }

        public static Task<IDispatchResult> DispatchAsync(this IMessageDispatcher messageDispatcher, Type messageType)
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

            return messageDispatcher.DispatchAsync(message);
        }
    }
}
