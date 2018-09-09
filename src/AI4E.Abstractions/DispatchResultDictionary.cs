using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AI4E
{
    public abstract class DispatchResultDictionary : IReadOnlyDictionary<string, object>, IDispatchResult
    {
        private readonly ImmutableDictionary<string, object> _data;

        private protected DispatchResultDictionary(IDispatchResult dispatchResult, IEnumerable<KeyValuePair<string, object>> data)
        {
            if (dispatchResult == null)
                throw new ArgumentNullException(nameof(dispatchResult));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            DispatchResult = dispatchResult;
            _data = data.ToImmutableDictionary();
        }

        public IDispatchResult DispatchResult { get; }

        #region IDispatchResult

        public bool IsSuccess => DispatchResult.IsSuccess;

        public string Message => DispatchResult.Message;

        #endregion

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

    public sealed class AggregateDispatchResultDictionary : DispatchResultDictionary, IAggregateDispatchResult
    {
        public AggregateDispatchResultDictionary(IAggregateDispatchResult dispatchResult, IEnumerable<KeyValuePair<string, object>> data)
            : base(dispatchResult, data)
        {
            DispatchResults = dispatchResult.DispatchResults;
        }

        public IEnumerable<IDispatchResult> DispatchResults { get; }

        public new IAggregateDispatchResult DispatchResult => (IAggregateDispatchResult)base.DispatchResult;
    }

    public sealed class DispatchResultDictionary<TDispatchResult> : DispatchResultDictionary
        where TDispatchResult : IDispatchResult
    {
        public DispatchResultDictionary(TDispatchResult dispatchResult)
            : base(dispatchResult, ImmutableDictionary<string, object>.Empty)
        { }

        public DispatchResultDictionary(TDispatchResult dispatchResult, IEnumerable<KeyValuePair<string, object>> data)
            : base(dispatchResult, data)
        { }

        public new TDispatchResult DispatchResult => (TDispatchResult)base.DispatchResult;
    }
}
