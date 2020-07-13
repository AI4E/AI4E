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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace AI4E.Messaging
{
    /// <summary>
    /// Describes the result of a message dispatch operation.
    /// </summary>
    [Serializable]
    public class DispatchResult : IDispatchResult, ISerializable
    {
        private readonly ImmutableDictionary<string, object?>? _resultData;
        private readonly bool? _isSuccess;

        #region C'tors

        private protected DispatchResult(
            bool? isSuccess = null,
            string? message = null,
            IReadOnlyDictionary<string, object?>? resultData = null)
        {
            _isSuccess = isSuccess;
            Message = message!;

            if (resultData == null)
            {
                ResultData = null!;
                return;
            }

            if (!(resultData is ImmutableDictionary<string, object?> immutableData))
            {
                immutableData = resultData.ToImmutableDictionary();
            }

            Debug.Assert(immutableData != null);
            _resultData = immutableData!;
            ResultData = new DispatchResultDictionary(immutableData!);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DispatchResult"/> type.
        /// </summary>
        /// <param name="isSuccess">A boolean value indicating whether the dispatch operation was successful.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        public DispatchResult(bool isSuccess, string message, IReadOnlyDictionary<string, object?> resultData)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (resultData == null)
                throw new ArgumentNullException(nameof(resultData));

            _isSuccess = isSuccess;
            Message = message;

            if (!(resultData is ImmutableDictionary<string, object?> immutableData))
            {
                immutableData = resultData.ToImmutableDictionary();
            }

            Debug.Assert(immutableData != null);
            _resultData = immutableData!;
            ResultData = new DispatchResultDictionary(immutableData!);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DispatchResult"/> type.
        /// </summary>
        /// <param name="isSuccess">A boolean value indicating whether the dispatch operation was successful.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException"> Thrown if <paramref name="message"/> is <c>null</c>. </exception>
        public DispatchResult(bool isSuccess, string message)
            : this(isSuccess, message, ImmutableDictionary<string, object?>.Empty)
        { }

        #endregion

        #region ISerializable

        protected DispatchResult(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            if (serializationInfo is null)
                throw new ArgumentNullException(nameof(serializationInfo));

            bool? isSuccess;
            string? message;
            ImmutableDictionary<string, object?>? resultData;

            try
            {
                isSuccess = serializationInfo.GetValue(nameof(IsSuccess), typeof(bool?)) as bool?;
                message = serializationInfo.GetString(nameof(Message));
                resultData = serializationInfo.GetValue(
                    nameof(ResultData), typeof(ImmutableDictionary<string, object?>))
                    as ImmutableDictionary<string, object?>;
            }
            catch (InvalidCastException exc)
            {
                // TODO: More specific error message
                throw new SerializationException("Cannot deserialize dispatch result.", exc);
            }

            _isSuccess = isSuccess;
            Message = message!;
            _resultData = resultData;
            ResultData = (resultData != null ? new DispatchResultDictionary(resultData) : null)!;

        }

        protected virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue(nameof(IsSuccess), _isSuccess, typeof(bool?));
            info.AddValue(nameof(Message), Message);
            info.AddValue(nameof(ResultData), _resultData, typeof(ImmutableDictionary<string, object?>));
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            GetObjectData(info, context);
        }

        #endregion

        /// <inheritdoc />
        public virtual bool IsSuccess => _isSuccess.GetValueOrDefault(false);

        /// <inheritdoc />
        public virtual string Message { get; }

        /// <inheritdoc />
        public virtual IReadOnlyDictionary<string, object?> ResultData { get; }

        protected virtual IReadOnlyDictionary<string, object?> RawResultData => _resultData ?? ImmutableDictionary<string, object?>.Empty;

        /// <inheritdoc />
        public sealed override string ToString()
        {
            var stringBuilder = new StringBuilder();
            FormatString(stringBuilder);
            return stringBuilder.ToString();
        }

        /// <summary>
        /// When overriden in a derived class, formats the dispatch result into the specified string builder.
        /// </summary>
        /// <param name="stringBuilder">A <see cref="StringBuilder"/> that contains the formatted dispatch result.</param>
        protected virtual void FormatString(StringBuilder stringBuilder)
        {
            if (stringBuilder is null)
                throw new ArgumentNullException(nameof(stringBuilder));

            stringBuilder.Append("Success: ");
            stringBuilder.Append(IsSuccess ? "true" : "false");

            // Message will never be null (except in an overridden case) but may be whitespace only.
            if (!string.IsNullOrWhiteSpace(Message))
            {
                stringBuilder.Append(" - ");
                stringBuilder.Append(Message);
            }
        }

        private sealed class DispatchResultDictionary : IReadOnlyDictionary<string, object?>
        {
            private readonly ImmutableDictionary<string, object?> _data;

            public DispatchResultDictionary(ImmutableDictionary<string, object?> data)
            {
                _data = data;
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
            }

            public IEnumerable<string> Keys => _data?.Keys ?? Enumerable.Empty<string>();

            public IEnumerable<object?> Values => _data?.Values ?? Enumerable.Empty<object>();

            public int Count => _data?.Count ?? 0;

            public bool ContainsKey(string key)
            {
                return key != null && _data != null && _data.ContainsKey(key);
            }

            public bool TryGetValue(string key, [NotNullWhen(true)] out object? value)
            {
                if (key == null || _data == null)
                {
                    value = default;
                    return false;
                }

                return _data.TryGetValue(key, out value) && value != null;
            }

            public Enumerator GetEnumerator()
            {
                if (_data is null)
                    return default;

                return new Enumerator(_data.GetEnumerator());
            }

            IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
            {
                var enumerable = _data as IEnumerable<KeyValuePair<string, object?>> ?? Enumerable.Empty<KeyValuePair<string, object?>>();

                return enumerable.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public struct Enumerator : IEnumerator<KeyValuePair<string, object?>>, IEnumerator
            {
                // This MUST NOT be marked readonly, to allow the compiler to access this field by reference.
                private ImmutableDictionary<string, object?>.Enumerator _underlying;

                public Enumerator(ImmutableDictionary<string, object?>.Enumerator underlying)
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
        }
    }
}
