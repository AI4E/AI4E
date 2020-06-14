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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using AI4E.Utils;

namespace AI4E.Messaging
{
    // This is inspired by the ViewDataDictionary, Asp.Net Core MVC uses to pass the view data from the controller to the view.
    // We have to use an immutable type however, to ensure consistency, as our messaging solution is not guaranteed to be used
    // by a single thread only.

    /// <summary>
    /// Contains the dispatch data of a dispatch operation.
    /// </summary>
#pragma warning disable CA1710
    [Serializable]
    public abstract class DispatchDataDictionary : IReadOnlyDictionary<string, object?>, ISerializable
#pragma warning restore CA1710
    {
        private protected readonly ImmutableDictionary<string, object?>? _data;

        #region C'tor

        // We cannot add a public constructor here. As the type is not sealed and cannot be (the generic version inherits from this type)
        // anyone could inherit from the type. We cannot ensure immutability in this case.
        // Normally this type is not created directly anyway but an instance of the derived (generic type is used) and this type is used 
        // only as cast target, if we do not know the message type.
        private protected DispatchDataDictionary(Type messageType, object message, IEnumerable<KeyValuePair<string, object?>> data)
        {
            ValidateArguments(messageType, message, data);

            Message = message;
            MessageType = messageType;
            _data = data as ImmutableDictionary<string, object?> ?? data.ToImmutableDictionary();
        }

        private static void ValidateArguments(Type messageType, object message, IEnumerable<KeyValuePair<string, object?>> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            ValidateArguments(messageType, message);
        }

        private static void ValidateArguments(Type messageType, object message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

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
        }

        #endregion

        #region ISerializable

#pragma warning disable CA2229
        private protected DispatchDataDictionary(SerializationInfo serializationInfo, StreamingContext streamingContext)
#pragma warning restore CA2229
        {
            if (serializationInfo is null)
                throw new ArgumentNullException(nameof(serializationInfo));

            var message = serializationInfo.GetValue(nameof(Message), typeof(object));

            if (message is null)
            {
                throw new SerializationException($"Unable to deserialize the {GetType().FullName}. There is no message specified.");
            }

            var messageType = serializationInfo.GetValue(nameof(MessageType), typeof(string)) as string;

            if (messageType is null)
            {
                throw new SerializationException($"Unable to deserialize the {GetType().FullName}. There is no message-type specified.");
            }

            Message = message;
            MessageType = TypeResolver.Default.ResolveType(messageType.AsSpan()); // TODO: Which type-resolver shall we use here?
            _data = serializationInfo.GetValueOrDefault<ImmutableDictionary<string, object?>>("Data");
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            GetObjectData(info, context);
        }

        protected void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue(nameof(Message), Message);
            info.AddValue(nameof(MessageType), MessageType.GetUnqualifiedTypeName());
            info.AddValue<ImmutableDictionary<string, object?>?>("Data", _data);
        }

        #endregion

        /// <summary>
        /// Gets the type of message that is dispatched.
        /// </summary>
        public Type MessageType { get; }

        /// <summary>
        /// Gets the message that is dispatched.
        /// </summary>
        public object Message { get; }

        #region DataDictionary

        /// <inheritdoc />
        public object? this[string key]
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
                    result = null!;
                }

                return result;
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> Keys => _data?.Keys ?? Enumerable.Empty<string>();

        /// <inheritdoc />
        public IEnumerable<object?> Values => _data?.Values ?? Enumerable.Empty<object?>();

        /// <inheritdoc />
        public int Count => _data?.Count ?? 0;

        /// <inheritdoc />
        public bool ContainsKey(string key)
        {
            return key != null && _data != null && _data.ContainsKey(key);
        }

        /// <inheritdoc />
        public bool TryGetValue(string key, [NotNullWhen(true)] out object? value)
        {
            if (key == null || _data == null)
            {
                value = default;
                return false;
            }

            return _data.TryGetValue(key, out value);
        }

        public Enumerator GetEnumerator()
        {
            if (_data is null)
            {
                return default;
            }

            return new Enumerator(_data.GetEnumerator());
        }

        IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
        {
            var enumerable = _data as IEnumerable<KeyValuePair<string, object?>> ?? Enumerable.Empty<KeyValuePair<string, object?>>();

            return enumerable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            var enumerable = _data as IEnumerable ?? Enumerable.Empty<KeyValuePair<string, object?>>();
            return enumerable.GetEnumerator();
        }

        #endregion

        #region Builder

        public static Builder CreateBuilder(Type messageType, object message)
        {
            ValidateArguments(messageType, message);
            return new Builder(messageType, message);
        }

        public static Builder CreateBuilder(object message)
        {
            return CreateBuilder(message?.GetType()!, message!);
        }

        public static DispatchDataDictionary<TMessage>.Builder CreateBuilder<TMessage>(TMessage message)
            where TMessage : class
        {
            return new DispatchDataDictionary<TMessage>.Builder(message);
        }

        public Builder ToBuilder()
        {
            return new Builder(
                MessageType,
                Message,
                _data?.ToBuilder() ?? ImmutableDictionary.CreateBuilder<string, object?>());
        }

#pragma warning disable CA1710, CA1034
        public class Builder : IDictionary<string, object?>
#pragma warning restore CA1034, CA1710
        {
            private readonly ImmutableDictionary<string, object?>.Builder _data;

            internal Builder(Type messageType, object message)
            {
                MessageType = messageType;
                Message = message;
                _data = ImmutableDictionary.CreateBuilder<string, object?>();
            }

            internal Builder(Type messageType, object message, ImmutableDictionary<string, object?>.Builder data)
            {
                MessageType = messageType;
                Message = message;
                _data = data;
            }

            public Type MessageType { get; }

            public object Message { get; }

            protected ImmutableDictionary<string, object?> BuildDataDictionary()
            {
                return _data.ToImmutable();
            }

            public DispatchDataDictionary BuildDispatchDataDictionary()
            {
                return Create(MessageType, Message, BuildDataDictionary());
            }

            #region DataDictionary

            public void Add(string key, object? value)
            {
                _data.Add(key, value);
            }

            public bool ContainsKey(string key)
            {
                return _data.ContainsKey(key);
            }

            public bool Remove(string key)
            {
                return _data.Remove(key);
            }

            public bool TryGetValue(string key, [NotNullWhen(true)] out object? value)
            {
                if (key == null || _data == null)
                {
                    value = default;
                    return false;
                }

                return _data.TryGetValue(key, out value);
            }

            public object? this[string key]
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
                set => _data[key] = value;
            }

            ICollection<string> IDictionary<string, object?>.Keys => ((IDictionary<string, object?>)_data).Keys;

            ICollection<object?> IDictionary<string, object?>.Values => ((IDictionary<string, object?>)_data).Values;

            public IEnumerable<string> Keys => _data.Keys;

            public IEnumerable<object?> Values => _data.Values;

            public void Add(KeyValuePair<string, object?> item)
            {
                _data.Add(item);
            }

            public void Clear()
            {
                _data.Clear();
            }

            public bool Contains(KeyValuePair<string, object?> item)
            {
                return _data.Contains(item);
            }

            public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
            {
                ((IDictionary<string, object?>)_data).CopyTo(array, arrayIndex);
            }

            public bool Remove(KeyValuePair<string, object?> item)
            {
                return _data.Remove(item);
            }

            public int Count => _data.Count;

#pragma warning disable CA1033
            bool ICollection<KeyValuePair<string, object?>>.IsReadOnly => ((IDictionary<string, object?>)_data).IsReadOnly;
#pragma warning restore CA1033

            public Enumerator GetEnumerator()
            {
                return new Enumerator(_data.GetEnumerator());
            }

            IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<string, object?>>)_data).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)_data).GetEnumerator();
            }

            #endregion
        }

        #endregion

        #region Enumerator

        public struct Enumerator : IEnumerator<KeyValuePair<string, object?>>, IEnumerator, IDisposable
        {
            // This MUST NOT be marked readonly, to allow the compiler to access this field by reference.
            private ImmutableDictionary<string, object?>.Enumerator _underlying;

            internal Enumerator(ImmutableDictionary<string, object?>.Enumerator underlying)
            {
                _underlying = underlying;
            }

            public KeyValuePair<string, object?> Current => _underlying.Current;

            object IEnumerator.Current => Current;


            public void Dispose()
            {
                _underlying.Dispose();
            }

            public bool MoveNext()
            {
                return _underlying.MoveNext();
            }

            public void Reset()
            {
                _underlying.Reset();
            }
        }

        #endregion

        #region Factory methods

        private static readonly Type _dispatchDataDictionaryTypeDefinition = typeof(DispatchDataDictionary<>);
        private static readonly ConditionalWeakTable<Type, Func<object, IEnumerable<KeyValuePair<string, object?>>, DispatchDataDictionary>> _factories
            = new ConditionalWeakTable<Type, Func<object, IEnumerable<KeyValuePair<string, object?>>, DispatchDataDictionary>>();

        private static readonly ConditionalWeakTable<Type, Func<object, IEnumerable<KeyValuePair<string, object?>>, DispatchDataDictionary>>.CreateValueCallback _buildFactory
            = BuildFactory; // Cache delegate for perf reasons.

        /// <summary>
        /// Creates an instance of the <see cref="DispatchDataDictionary"/> type.
        /// </summary>
        /// <param name="messageType">The type of message that is dispatched.</param>
        /// <param name="message">The message that is dispatched.</param>
        /// <returns>The created <see cref="DispatchDataDictionary"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageType"/> or <paramref name="message"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if either the type of <paramref name="message"/> is not assignable to <paramref name="messageType"/> or
        /// <paramref name="messageType"/> is not a valid message type.
        /// </exception>
        public static DispatchDataDictionary Create(Type messageType, object message)
        {
            return Create(messageType, message, ImmutableDictionary<string, object?>.Empty);
        }

        /// <summary>
        /// Creates an instance of the <see cref="DispatchDataDictionary"/> type.
        /// </summary>
        /// <param name="messageType">The type of message that is dispatched.</param>
        /// <param name="message">The message that is dispatched.</param>
        /// <param name="data">A collection of key value pairs that contain additional dispatch data.</param>
        /// <returns>The created <see cref="DispatchDataDictionary"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of  <paramref name="messageType"/>, <paramref name="message"/> or <paramref name="data"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if either the type of <paramref name="message"/> is not assignable to <paramref name="messageType"/> or
        /// <paramref name="messageType"/> is not a valid message type.
        /// </exception>
        public static DispatchDataDictionary Create(Type messageType, object message, IEnumerable<KeyValuePair<string, object?>> data)
        {
            ValidateArguments(messageType, message, data);

            var factory = _factories.GetValue(messageType, _buildFactory);
            return factory(message, data);
        }

        /// <summary>
        /// Creates an instance of the <see cref="DispatchDataDictionary"/> type.
        /// </summary>
        /// <param name="message">The message that is dispatched.</param>
        /// <returns>The created <see cref="DispatchDataDictionary"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the type of <paramref name="message"/> is not a valid message type.
        /// </exception>
        public static DispatchDataDictionary Create(object message)
        {
            return Create(message?.GetType()!, message!, ImmutableDictionary<string, object?>.Empty);
        }

        /// <summary>
        /// Creates an instance of the <see cref="DispatchDataDictionary"/> type.
        /// </summary>
        /// <param name="message">The message that is dispatched.</param>
        /// <param name="data">A collection of key value pairs that contain additional dispatch data.</param>
        /// <returns>The created <see cref="DispatchDataDictionary"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="data"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the type of <paramref name="message"/> is not a valid message type.
        /// </exception>
        public static DispatchDataDictionary Create(object message, IEnumerable<KeyValuePair<string, object?>> data)
        {
            return Create(message?.GetType()!, message!, data);
        }

        private static Func<object, IEnumerable<KeyValuePair<string, object?>>, DispatchDataDictionary> BuildFactory(Type messageType)
        {
            var dispatchDataDictionaryType = _dispatchDataDictionaryTypeDefinition.MakeGenericType(messageType);

            Debug.Assert(dispatchDataDictionaryType != null);

            var ctor = dispatchDataDictionaryType!.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                Type.DefaultBinder,
                new Type[] { messageType, typeof(IEnumerable<KeyValuePair<string, object?>>) },
                modifiers: null);

            Debug.Assert(ctor != null);

            var messageParameter = Expression.Parameter(typeof(object), "message");
            var dataParameter = Expression.Parameter(typeof(IEnumerable<KeyValuePair<string, object?>>), "data");
            var convertedMessage = Expression.Convert(messageParameter, messageType);
            var ctorCall = Expression.New(ctor, convertedMessage, dataParameter);
            var convertedResult = Expression.Convert(ctorCall, typeof(DispatchDataDictionary));
            var lambda = Expression.Lambda<Func<object, IEnumerable<KeyValuePair<string, object?>>, DispatchDataDictionary>>(
                convertedResult,
                messageParameter,
                dataParameter);

            return lambda.Compile();
        }

        #endregion
    }

    /// <summary>
    /// Contains the dispatch data of a dispatch operation.
    /// </summary>
    /// <typeparam name="TMessage">The type of message that is dispatched.</typeparam> 
#pragma warning disable CA1710
    [Serializable]
    public sealed class DispatchDataDictionary<TMessage> : DispatchDataDictionary
#pragma warning restore CA1710
        where TMessage : class
    {
        /// <summary>
        /// Creates an instance of the <see cref="DispatchDataDictionary"/> type.
        /// </summary>
        /// <param name="message">The message that is dispatched.</param>
        /// <param name="data">A collection of key value pairs that contain additional dispatch data.</param>
        /// <returns>The created <see cref="DispatchDataDictionary"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="data"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <typeparamref name="TMessage"/> is not a valid message type.
        /// </exception>
        public DispatchDataDictionary(TMessage message, IEnumerable<KeyValuePair<string, object?>> data)
            : base(typeof(TMessage), message, data)
        { }

        /// <summary>
        /// Creates an instance of the <see cref="DispatchDataDictionary"/> type.
        /// </summary>
        /// <param name="message">The message that is dispatched.</param>
        /// <returns>The created <see cref="DispatchDataDictionary"/>.</returns>
        /// <exception cref="ArgumentNullException"> Thrown if <paramref name="message"/> is null. </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <typeparamref name="TMessage"/> is not a valid message type.
        /// </exception>
        public DispatchDataDictionary(TMessage message)
            : base(typeof(TMessage), message, ImmutableDictionary<string, object?>.Empty)
        { }

        public DispatchDataDictionary(Type messageType, TMessage message, IEnumerable<KeyValuePair<string, object?>> data)
            : base(messageType, message, data)
        {
            ValidateMessageType(messageType);
        }

        public DispatchDataDictionary(Type messageType, TMessage message)
            : base(messageType, message, ImmutableDictionary<string, object?>.Empty)
        {
            ValidateMessageType(messageType);
        }

        private static void ValidateMessageType(Type messageType)
        {
            if (!typeof(TMessage).IsAssignableFrom(messageType))
                throw new ArgumentException($"The specified message-type must be of type '{ typeof(TMessage) }' or a derived type.", nameof(messageType));
        }

        private DispatchDataDictionary(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        { }

        /// <summary>
        /// Gets the message that is dispatched.
        /// </summary>
        public new TMessage Message => (TMessage)base.Message;

        #region Builder

        public new Builder ToBuilder()
        {
            return new Builder(
                Message,
                _data?.ToBuilder() ?? ImmutableDictionary.CreateBuilder<string, object?>());
        }

#pragma warning disable CA1710, CA1034
        public new class Builder : DispatchDataDictionary.Builder
#pragma warning restore CA1034, CA1710
        {
            internal Builder(TMessage message)
                : base(typeof(TMessage), message)
            {
                Message = message;
            }

            internal Builder(TMessage message, ImmutableDictionary<string, object?>.Builder data)
                : base(typeof(TMessage), message, data)
            {
                Message = message;
            }

            public new TMessage Message { get; }

            public new DispatchDataDictionary<TMessage> BuildDispatchDataDictionary()
            {
                return new DispatchDataDictionary<TMessage>(MessageType, Message, BuildDataDictionary());
            }
        }

        #endregion
    }
}
