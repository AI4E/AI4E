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
using System.Threading;
using AI4E.Utils;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.DispatchResults
{
    /// <summary>
    /// Aggregates multiple message dispatch results to a single result.
    /// </summary>
    public sealed class AggregateDispatchResult : DispatchResult, IAggregateDispatchResult
    {
        [JsonProperty("ResultData")]
#pragma warning disable IDE0052 // This is needed for serialization
        private readonly ImmutableDictionary<string, object> _resultData;
#pragma warning restore IDE0052 

        /// <summary>
        /// Creates a new instance of type <see cref="AggregateDispatchResult"/>.
        /// </summary>
        /// <param name="dispatchResults">The collection of dispatch results.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="dispatchResults"/> or <paramref name="resultData"/> is null.
        /// </exception>
        [JsonConstructor]
        public AggregateDispatchResult(IEnumerable<IDispatchResult> dispatchResults, IReadOnlyDictionary<string, object> resultData)
        {
            if (dispatchResults == null)
                throw new ArgumentNullException(nameof(dispatchResults));

            if (resultData == null)
                throw new ArgumentNullException(nameof(resultData));

            if (dispatchResults.Any(p => p is null))
            {
                DispatchResults = dispatchResults.Where(p => !(p is null)).ToImmutableList();
            }
            else
            {
                if (!(dispatchResults is ImmutableList<IDispatchResult> immutableResults))
                {
                    immutableResults = dispatchResults.ToImmutableList();
                }

                DispatchResults = immutableResults;
            }

            if (!(resultData is ImmutableDictionary<string, object> immutableData))
            {
                immutableData = resultData.ToImmutableDictionary();
            }

            Assert(immutableData != null);
            _resultData = immutableData;

            ResultData = new AggregateDispatchResultDataDictionary((ImmutableList<IDispatchResult>)DispatchResults, immutableData);
        }

        /// <summary>
        /// Creates a new instance of type <see cref="AggregateDispatchResult"/>.
        /// </summary>
        /// <param name="dispatchResults">The collection of dispatch results.</param>
        /// <exception cref="ArgumentNullException"> Thrown if <paramref name="dispatchResults"/> is null. </exception>
        public AggregateDispatchResult(IEnumerable<IDispatchResult> dispatchResults)
            : this(dispatchResults, ImmutableDictionary<string, object>.Empty)
        { }

        /// <summary>
        /// Creates a new instance of type <see cref="AggregateDispatchResult"/>.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result that shall be added new result data.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="dispatchResult"/> or <paramref name="resultData"/> is null.
        /// </exception>
        public AggregateDispatchResult(IDispatchResult dispatchResult, IReadOnlyDictionary<string, object> resultData)
            : this((dispatchResult ?? throw new ArgumentNullException(nameof(dispatchResult))).Yield(), resultData)
        { }

        /// <inheritdoc />
        public IEnumerable<IDispatchResult> DispatchResults { get; }

        /// <inheritdoc />
        [JsonIgnore]
        public override bool IsSuccess => DispatchResults.Count() == 0 || DispatchResults.All(p => p.IsSuccess);

        /// <inheritdoc />
        [JsonIgnore]
        public override string Message
        {
            get
            {
                var count = DispatchResults.Count();

                if (count == 0)
                {
                    return "{ No results }";
                }

                if (count == 1)
                {
                    return DispatchResults.First().Message;
                }

                return "{ Multiple results }";
            }
        }

        /// <inheritdoc />
        [JsonIgnore]
        public override IReadOnlyDictionary<string, object> ResultData { get; }

        private sealed class AggregateDispatchResultDataDictionary : IReadOnlyDictionary<string, object>
        {
            private readonly ImmutableList<IDispatchResult> _dispatchResults;
            private readonly ImmutableDictionary<string, object> _resultData;

            // This is generated only when needed.
            private readonly Lazy<ImmutableDictionary<string, object>> _combinedResultData;

            public AggregateDispatchResultDataDictionary(
                ImmutableList<IDispatchResult> dispatchResults,
                ImmutableDictionary<string, object> resultData)
            {
                _dispatchResults = dispatchResults;
                _resultData = resultData;
                _combinedResultData = new Lazy<ImmutableDictionary<string, object>>(CreateCombinedResultData, LazyThreadSafetyMode.PublicationOnly);
            }

            public bool ContainsKey(string key)
            {
                return TryGetValue(key, out _);
            }

            public bool TryGetValue(string key, out object value)
            {
                value = null;

                if (key == null)
                    return false;

                if (_combinedResultData.IsValueCreated)
                {
                    return _combinedResultData.Value.TryGetValue(key, out value);
                }

                if (_resultData != null && _resultData.TryGetValue(key, out value))
                {
                    return value != null;
                }

                foreach (var dispatchResult in _dispatchResults)
                {
                    var resultData = dispatchResult.ResultData;
                    if (resultData != null && resultData.TryGetValue(key, out value) && value != null)
                    {
                        return true;
                    }
                }

                return false;
            }

            public object this[string key]
            {
                get
                {
                    if (!TryGetValue(key, out var result))
                    {
                        result = null;
                    }

                    return result;
                }
            }

            public IEnumerable<string> Keys => _combinedResultData.Value.Keys;

            public IEnumerable<object> Values => _combinedResultData.Value.Values;

            public int Count => _combinedResultData.Value.Count;

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                return _combinedResultData.Value.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private ImmutableDictionary<string, object> CreateCombinedResultData()
            {
                var builder = (_resultData ?? ImmutableDictionary<string, object>.Empty).ToBuilder();

                foreach (var dispatchResult in _dispatchResults)
                {
                    var resultData = dispatchResult.ResultData;
                    if (resultData == null)
                    {
                        continue;
                    }

                    foreach (var kvp in resultData)
                    {
                        if (builder.ContainsKey(kvp.Key))
                        {
                            continue;
                        }

                        if (kvp.Value == null)
                        {
                            continue;
                        }

                        builder.Add(kvp);
                    }
                }

                builder.RemoveRange((_resultData ?? ImmutableDictionary<string, object>.Empty).Where(p => p.Value == null).Select(p => p.Key));

#if DEBUG
                Assert(!builder.Any(p => p.Value == null));
#endif

                return builder.ToImmutable();
            }
        }
    }
}
