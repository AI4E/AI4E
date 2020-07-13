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
using System.Threading;

namespace AI4E.Messaging
{
    /// <summary>
    /// Aggregates multiple message dispatch results to a single result.
    /// </summary>
    [Serializable]
    public sealed class AggregateDispatchResult : DispatchResult, IAggregateDispatchResult
    {
        private readonly ImmutableList<IDispatchResult> _dispatchResults;

        #region C'tors

        /// <summary>
        /// Creates a new instance of type <see cref="AggregateDispatchResult"/>.
        /// </summary>
        /// <param name="dispatchResults">The collection of dispatch results.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="dispatchResults"/> or <paramref name="resultData"/> is null.
        /// </exception>
        public AggregateDispatchResult(
            IEnumerable<IDispatchResult> dispatchResults, 
            IReadOnlyDictionary<string, object?> resultData) : base(resultData: resultData)
        {
            if (dispatchResults == null)
                throw new ArgumentNullException(nameof(dispatchResults));

            if (resultData == null)
                throw new ArgumentNullException(nameof(resultData));

            if (dispatchResults.Any(p => p is null))
            {
                _dispatchResults = dispatchResults.Where(p => !(p is null)).ToImmutableList();
            }
            else
            {
                if (!(dispatchResults is ImmutableList<IDispatchResult> immutableResults))
                {
                    immutableResults = dispatchResults.ToImmutableList();
                }

                _dispatchResults = immutableResults;
            }

            ResultData = new AggregateDispatchResultDataDictionary(_dispatchResults, RawResultData);
        }

        /// <summary>
        /// Creates a new instance of type <see cref="AggregateDispatchResult"/>.
        /// </summary>
        /// <param name="dispatchResults">The collection of dispatch results.</param>
        /// <exception cref="ArgumentNullException"> Thrown if <paramref name="dispatchResults"/> is null. </exception>
        public AggregateDispatchResult(IEnumerable<IDispatchResult> dispatchResults)
            : this(dispatchResults, ImmutableDictionary<string, object?>.Empty)
        { }

        /// <summary>
        /// Creates a new instance of type <see cref="AggregateDispatchResult"/>.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result that shall be added new result data.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="dispatchResult"/> or <paramref name="resultData"/> is null.
        /// </exception>
        public AggregateDispatchResult(IDispatchResult dispatchResult, IReadOnlyDictionary<string, object?> resultData)
            : this((dispatchResult ?? throw new ArgumentNullException(nameof(dispatchResult))).Yield(), resultData)
        { }

        #endregion

        private AggregateDispatchResult(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
            ImmutableList<IDispatchResult>? dispatchResults;

            try
            {
#pragma warning disable CA1062
                dispatchResults = serializationInfo.GetValue(
                    nameof(DispatchResults), typeof(ImmutableList<IDispatchResult>)) as ImmutableList<IDispatchResult>;
#pragma warning restore CA1062
            }
            catch (InvalidCastException exc)
            {
                // TODO: More specific error message
                throw new SerializationException("Cannot deserialize dispatch result.", exc);
            }

            if (dispatchResults is null)
            {
                // TODO: More specific error message
                throw new SerializationException("Cannot deserialize dispatch result.");
            }

            _dispatchResults = dispatchResults;
            ResultData = new AggregateDispatchResultDataDictionary(
                (ImmutableList<IDispatchResult>)DispatchResults, 
                RawResultData);
        }

        protected override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
#pragma warning disable CA1062
            info.AddValue(nameof(DispatchResults), DispatchResults, typeof(ImmutableList<IDispatchResult>));
#pragma warning restore CA1062
        }

        /// <inheritdoc />
        public IEnumerable<IDispatchResult> DispatchResults => _dispatchResults;

        /// <inheritdoc />
        public override bool IsSuccess => !DispatchResults.Any() || DispatchResults.All(p => p.IsSuccess);

        /// <inheritdoc />
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
        public override IReadOnlyDictionary<string, object?> ResultData { get; }
        private sealed class AggregateDispatchResultDataDictionary : IReadOnlyDictionary<string, object?>
        {
            private readonly ImmutableList<IDispatchResult> _dispatchResults;
            private readonly IReadOnlyDictionary<string, object?> _resultData;

            // This is generated only when needed.
            private readonly Lazy<ImmutableDictionary<string, object>> _combinedResultData;

            public AggregateDispatchResultDataDictionary(
                ImmutableList<IDispatchResult> dispatchResults,
                IReadOnlyDictionary<string, object?> resultData)
            {
                _dispatchResults = dispatchResults;
                _resultData = resultData;
                _combinedResultData = new Lazy<ImmutableDictionary<string, object>>(
                    CreateCombinedResultData, 
                    LazyThreadSafetyMode.PublicationOnly);
            }

            public bool ContainsKey(string key)
            {
                return TryGetValue(key, out _);
            }

            public bool TryGetValue(string key, [NotNullWhen(true)] out object? value)
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

            public object? this[string key]
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

            public IEnumerable<object?> Values => _combinedResultData.Value.Values;

            public int Count => _combinedResultData.Value.Count;

            public Enumerator GetEnumerator()
            {
                return new Enumerator(_combinedResultData.Value.GetEnumerator()!);
            }

            IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<string, object?>>)_combinedResultData.Value!).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)_combinedResultData.Value).GetEnumerator();
            }

            private ImmutableDictionary<string, object?>.Builder GetBuilder()
            {
                if (_resultData is ImmutableDictionary<string, object?> immutableDictionary)
                    return immutableDictionary.ToBuilder();

                var result = ImmutableDictionary.CreateBuilder<string, object?>();

                if (_resultData != null)
                {
                    result.AddRange(_resultData);
                }

                return result;
            }

            private ImmutableDictionary<string, object> CreateCombinedResultData()
            {
                var builder = GetBuilder();

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

                builder.RemoveRange(
                    (_resultData ?? ImmutableDictionary<string, object?>.Empty)
                    .Where(p => p.Value == null)
                    .Select(p => p.Key));

#if DEBUG
                Debug.Assert(!builder.Any(p => p.Value == null));
#endif

                return builder.ToImmutable()!;
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
