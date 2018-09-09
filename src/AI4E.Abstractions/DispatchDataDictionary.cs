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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Internal;

namespace AI4E
{
    // This is inspired by the ViewDataDictionary, Asp.Net Core MVC uses to pass the view data from the controller to the view.
    // We have to use an immutable type however, to ensure consistency, as our messaging solution is not guaranteed to be used
    // by a single thread only.
    public abstract class DispatchDataDictionary : IReadOnlyDictionary<string, object>
    {
        private readonly ImmutableDictionary<string, object> _data;

        #region C'tor

        // We cannot add a public constructor here. As the type is not sealed and cannot be (the generic version inherits from this type)
        // anyone could inherit from the type. We cannot ensure immutability in this case.
        // Normally this type is not created directly anyway but an instance of the derived (generic type is used) and this type is used 
        // only as cast target, if we do not know the message type.
        private protected DispatchDataDictionary(object message, Type messageType, IEnumerable<KeyValuePair<string, object>> data)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (messageType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(messageType));

            if (messageType.IsDelegate())
                throw new ArgumentException("The argument must not specify a delegate type.", nameof(messageType));

            if (messageType.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be an open generic type definition.", nameof(messageType));

            // TODO: Do we have to check for System.Void? It is defined as a value type, that we alread check for.
            if (messageType == typeof(Enum) || messageType == typeof(ValueType) || messageType == typeof(void))
                throw new ArgumentException("The argument must not be one of the special types 'System.Enum', 'System.ValueType' or 'System.Void'.", nameof(messageType));

            if (!messageType.IsAssignableFrom(message.GetType()))
                throw new ArgumentException($"The specified message must be of type '{ messageType }' or a derived type.");

            // Altough we already checked whether message Type is neither a value type nor a delegate, 
            // it is possible that messageType is System.Object and the message is a delegate or a value type.
            if (messageType == typeof(object))
            {
                var actualMessageType = message.GetType();
                if (actualMessageType.IsValueType)
                {
                    throw new ArgumentException("The argument must be a reference type.", nameof(message));
                }

                if (actualMessageType.IsDelegate())
                {
                    throw new ArgumentException("The argument must not be a delegate.", nameof(message));
                }
            }

            MessageType = messageType;
            Message = message;
            _data = data.ToImmutableDictionary();
        }

        #endregion

        public Type MessageType { get; }
        public object Message { get; }

        #region IReadOnlyDictionary<string, object>

        public object this[string key]
        {
            get
            {
                // Do not pass through to _data as we do not want to throw a KeyNotFoundException
                if (key == null || _data == null)
                {
                    return null;
                }

                if (!_data.TryGetValue(key, out var result))
                {
                    result = null;
                }

                return result;
            }
        }

        public IEnumerable<string> Keys => _data?.Keys ?? Enumerable.Empty<string>();

        public IEnumerable<object> Values => _data?.Values ?? Enumerable.Empty<object>();

        public int Count => _data?.Count ?? 0;

        public bool ContainsKey(string key)
        {
            return key != null && _data != null && _data.ContainsKey(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            if (key == null || _data == null)
            {
                value = default;
                return false;
            }

            return _data.TryGetValue(key, out value);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            var enumerable = _data as IEnumerable<KeyValuePair<string, object>> ?? Enumerable.Empty<KeyValuePair<string, object>>();

            return enumerable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }

    public sealed class DispatchDataDictionary<TMessage> : DispatchDataDictionary
        where TMessage : class
    {
        public DispatchDataDictionary(TMessage message, IEnumerable<KeyValuePair<string, object>> data)
            : base(message, typeof(TMessage), data)
        { }

        public DispatchDataDictionary(TMessage message)
            : base(message, typeof(TMessage), ImmutableDictionary<string, object>.Empty)
        { }

        public new TMessage Message => (TMessage)base.Message;
    }
}
