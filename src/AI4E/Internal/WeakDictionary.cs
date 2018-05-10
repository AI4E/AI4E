using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AI4E.Internal
{
    public sealed class WeakDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
        where TValue : class
    {
        private readonly ConcurrentDictionary<TKey, WeakReference<TValue>> _entries;
        private readonly ConcurrentQueue<TKey> _cleanupQueue = new ConcurrentQueue<TKey>();
        private readonly Finalizer<TValue> _finalizer = new Finalizer<TValue>();

        public WeakDictionary()
        {
            _entries = new ConcurrentDictionary<TKey, WeakReference<TValue>>();
        }

        public WeakDictionary(IEqualityComparer<TKey> equalityComparer)
        {
            if (equalityComparer == null)
                throw new ArgumentNullException(nameof(equalityComparer));

            _entries = new ConcurrentDictionary<TKey, WeakReference<TValue>>(equalityComparer);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            Cleanup();

            value = default;

            return _entries.TryGetValue(key, out var weakReference) &&
                   weakReference.TryGetTarget(out value);
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            Cleanup();
            return GetOrAddInternal(key, factory);
        }

        public TValue GetOrAdd(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Cleanup();
            return GetOrAddInternal(key, _ => value);
        }

        public bool TryRemove(TKey key, out TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            Cleanup();

            value = default;
            return _entries.TryRemove(key, out var weakReference) && weakReference.TryGetTarget(out value);
        }

        public bool TryRemove(TKey key, TValue comparand)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (comparand == null)
                throw new ArgumentNullException(nameof(comparand));

            Cleanup();

            WeakReference<TValue> weakReference;

            do
            {
                if (!_entries.TryGetValue(key, out weakReference) || !weakReference.TryGetTarget(out var value) || !value.Equals(comparand))
                {
                    return false;
                }
            }
            while (!_entries.TryRemove(key, weakReference));

            return true;
        }

        private TValue GetOrAddInternal(TKey key, Func<TKey, TValue> factory)
        {
            var newValue = default(TValue);
            var newWeakReference = default(WeakReference<TValue>);
            var cleanCallback = default(Action<TValue>);

            do
            {
                if (_entries.TryGetValue(key, out var weakReference))
                {
                    if (weakReference.TryGetTarget(out var value))
                    {
                        if (newValue != null)
                        {
                            _finalizer.RemoveHandler(newValue, cleanCallback);
                        }

                        return value;
                    }

                    if (newValue == null)
                    {
                        (newValue, newWeakReference, cleanCallback) = CreateValue(key, factory);
                    }

                    if (_entries.TryUpdate(key, newWeakReference, weakReference))
                    {
                        return newValue;
                    }
                }
                else
                {
                    if (newValue == null)
                    {
                        (newValue, newWeakReference, cleanCallback) = CreateValue(key, factory);
                    }

                    if (_entries.TryAdd(key, newWeakReference))
                    {
                        return newValue;
                    }
                }
            }
            while (true);
        }

        private (TValue newValue, WeakReference<TValue> newWeakReference, Action<TValue> cleanCallback) CreateValue(TKey key, Func<TKey, TValue> factory)
        {
            var newValue = factory(key);

            if (newValue == null)
            {
                throw new InvalidOperationException($"The value provided by '{nameof(factory)}' must not be null.");
            }

            var newWeakReference = new WeakReference<TValue>(newValue);

#pragma warning disable IDE0039

            Action<TValue> cleanCallback = _ => _cleanupQueue.Enqueue(key);

#pragma warning restore IDE0039

            _finalizer.AddHandler(newValue, cleanCallback);

            return (newValue, newWeakReference, cleanCallback);
        }

        private void Cleanup()
        {
            while (_cleanupQueue.TryDequeue(out var key))
            {
                while (_entries.TryGetValue(key, out var weakReference) && !weakReference.TryGetTarget(out _) && !_entries.TryRemove(key, weakReference)) ;
            }
        }

        #region IEnumerable

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var entry in _entries)
            {
                if (entry.Value.TryGetTarget(out var value))
                {
                    yield return new KeyValuePair<TKey, TValue>(entry.Key, value);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }

    public sealed class Finalizer<T> where T : class
    {
        private readonly ConditionalWeakTable<T, SingleFinalizer> _weakTable = new ConditionalWeakTable<T, SingleFinalizer>();

        public void AddHandler(T value, Action<T> action)
        {
            var finalizer = _weakTable.GetValue(value, _ => new SingleFinalizer(value));

            finalizer.AddHandler(action);
        }

        public void RemoveHandler(T value, Action<T> action)
        {
            if (_weakTable.TryGetValue(value, out var finalizer))
            {
                finalizer.RemoveHandler(action);
            }
        }

        private sealed class SingleFinalizer
        {
            private readonly T _value;
            private volatile Action<T> _action;

            public SingleFinalizer(T value)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _value = value;
            }

            public void AddHandler(Action<T> action)
            {
                Action<T> current = _action, // Volatile read op
                          start,
                          desired;

                do
                {
                    start = current;

                    desired = start + action;

                    current = Interlocked.CompareExchange(ref _action, desired, start);
                }
                while (start != current);
            }

            public void RemoveHandler(Action<T> action)
            {
                Action<T> current = _action, // Volatile read op
                          start,
                          desired;

                do
                {
                    start = current;

                    desired = start - action;

                    current = Interlocked.CompareExchange(ref _action, desired, start);
                }
                while (start != current);
            }

            ~SingleFinalizer()
            {
                _action?.Invoke(_value); // Volatile read op
            }
        }

    }
}
