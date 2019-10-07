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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AI4E.Utils
{
#pragma warning disable CA1710
    public sealed class WeakDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
#pragma warning restore CA1710
        where TValue : class
        where TKey : notnull
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

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] [NotNullWhen(true)] out TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            Cleanup();

            value = default!;

            return _entries.TryGetValue(key, out var weakReference) &&
                   weakReference.TryGetTarget(out value!);
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

        public bool TryRemove(TKey key, [MaybeNullWhen(false)] [NotNullWhen(true)] out TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            Cleanup();

            value = default!;
            return _entries.TryRemove(key, out var weakReference)
                && weakReference.TryGetTarget(out value!);
        }

        public bool TryRemove(TKey key, TValue comparand)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (comparand == null)
                throw new ArgumentNullException(nameof(comparand));

            Cleanup();

            WeakReference<TValue>? weakReference;

            do
            {
                if (!_entries.TryGetValue(key, out weakReference)
                    || !weakReference.TryGetTarget(out var value)
                    || !value.Equals(comparand))
                {
                    return false;
                }
            }
            while (!_entries.Remove(key, weakReference));

            return true;
        }

        private TValue GetOrAddInternal(TKey key, Func<TKey, TValue> factory)
        {
            TValue? newValue = null;
            WeakReference<TValue>? newWeakReference = null;
            Action<TValue>? cleanCallback = null;
            var valueCreated = false;

            while (true)
            {
                if (_entries.TryGetValue(key, out var weakReference))
                {
                    if (weakReference.TryGetTarget(out var value))
                    {
                        if (valueCreated)
                        {
                            _finalizer.RemoveHandler(newValue!, cleanCallback!);
                        }

                        return value;
                    }

                    if (!valueCreated)
                    {
                        (newValue, newWeakReference, cleanCallback) = CreateValue(key, factory);
                        valueCreated = true;
                    }

                    if (_entries.TryUpdate(key, newWeakReference!, weakReference))
                    {
                        return newValue!;
                    }
                }
                else
                {
                    if (!valueCreated)
                    {
                        (newValue, newWeakReference, cleanCallback) = CreateValue(key, factory);
                        valueCreated = true;
                    }

                    if (_entries.TryAdd(key, newWeakReference!))
                    {
                        return newValue!;
                    }
                }
            }
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
                while (_entries.TryGetValue(key, out var weakReference) && !weakReference.TryGetTarget(out _) && !_entries.Remove(key, weakReference)) ;
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
            private volatile Action<T>? _action;

            public SingleFinalizer(T value)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _value = value;
            }

            public void AddHandler(Action<T> action)
            {
                Action<T>? current = _action, // Volatile read op
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
                Action<T>? current = _action, // Volatile read op
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
