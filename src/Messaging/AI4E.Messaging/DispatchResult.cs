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
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Messaging
{
    /// <summary>
    /// Describes the result of a message dispatch operation.
    /// </summary>
    public class DispatchResult : IDispatchResult
    {
        [JsonProperty("ResultData")]
#pragma warning disable IDE0052 // This is needed to support serialization.
        private readonly ImmutableDictionary<string, object> _resultData;
#pragma warning restore IDE0052 

        private protected DispatchResult() { }

        /// <summary>
        /// Creates a new instance of the <see cref="DispatchResult"/> type.
        /// </summary>
        /// <param name="isSuccess">A boolean value indicating whether the dispatch operation was successful.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        [JsonConstructor]
        public DispatchResult(bool isSuccess, string message, IReadOnlyDictionary<string, object> resultData)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (resultData == null)
                throw new ArgumentNullException(nameof(resultData));

            IsSuccess = isSuccess;
            Message = message;

            if (!(resultData is ImmutableDictionary<string, object> immutableData))
            {
                immutableData = resultData.ToImmutableDictionary();
            }

            Assert(immutableData != null);
            _resultData = immutableData;


            ResultData = new DispatchResultDictionary(immutableData);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DispatchResult"/> type.
        /// </summary>
        /// <param name="isSuccess">A boolean value indicating whether the dispatch operation was successful.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException"> Thrown if <paramref name="message"/> is <c>null</c>. </exception>
        public DispatchResult(bool isSuccess, string message)
            : this(isSuccess, message, ImmutableDictionary<string, object>.Empty)
        { }

        /// <inheritdoc />
        public virtual bool IsSuccess { get; }

        /// <inheritdoc />
        public virtual string Message { get; }

        /// <inheritdoc />
        [JsonIgnore]
        public virtual IReadOnlyDictionary<string, object> ResultData { get; }

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
            stringBuilder.Append("Success: ");
            stringBuilder.Append(IsSuccess ? "true" : "false");

            // Message will never be null (except in an overridden case) but may be whitespace only.
            if (!string.IsNullOrWhiteSpace(Message))
            {
                stringBuilder.Append(" - ");
                stringBuilder.Append(Message);
            }
        }

        private sealed class DispatchResultDictionary : IReadOnlyDictionary<string, object>
        {
            private readonly ImmutableDictionary<string, object> _data;

            public DispatchResultDictionary(ImmutableDictionary<string, object> data)
            {
                _data = data;
            }

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
        }
    }
}
