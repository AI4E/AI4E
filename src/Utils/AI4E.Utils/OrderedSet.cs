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

namespace AI4E.Utils
{
    // TODO: Implement ISet<T>
    // Adapted from: https://stackoverflow.com/questions/1552225/hashset-that-preserves-ordering#answer-17853085
#pragma warning disable CA1710
    public class OrderedSet<T> : ICollection<T>, IReadOnlyCollection<T>
        where T : notnull
#pragma warning restore CA1710
    {
        private readonly IDictionary<KeyWrapper, LinkedListNode<T>> _dictionary;
        private readonly LinkedList<T> _linkedList;

        public OrderedSet()
            : this(EqualityComparer<T>.Default)
        { }

        public OrderedSet(IEqualityComparer<T> comparer)
        {
            var keyComparer = new KeyWrapperEqualityComparer(comparer);

            _dictionary = new Dictionary<KeyWrapper, LinkedListNode<T>>(keyComparer);
            _linkedList = new LinkedList<T>();
        }

        public int Count => _dictionary.Count;

        public virtual bool IsReadOnly => _dictionary.IsReadOnly;

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public bool Add(T item)
        {
            if (_dictionary.ContainsKey(new KeyWrapper(item)))
            {
                return false;
            }

            var node = _linkedList.AddLast(item);
            _dictionary.Add(new KeyWrapper(item), node);
            return true;
        }

        public void Clear()
        {
            _linkedList.Clear();
            _dictionary.Clear();
        }

        public bool Remove(T item)
        {
            if (!_dictionary.Remove(new KeyWrapper(item), out var node))
            {
                return false;
            }

            _linkedList.Remove(node);
            return true;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_linkedList.GetEnumerator());
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return _linkedList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _linkedList.GetEnumerator();
        }

        public bool Contains(T item)
        {
            return _dictionary.ContainsKey(new KeyWrapper(item));
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _linkedList.CopyTo(array, arrayIndex);
        }

        public readonly struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator
        {
            private readonly LinkedList<T>.Enumerator _linkedListEnumerator;

            internal Enumerator(LinkedList<T>.Enumerator linkedListEnumerator)
            {
                _linkedListEnumerator = linkedListEnumerator;
            }

            public bool MoveNext()
            {
                return _linkedListEnumerator.MoveNext();
            }

            void IEnumerator.Reset()
            {
                ((IEnumerator<T>)_linkedListEnumerator).Reset();
            }

            public T Current => _linkedListEnumerator.Current;

            object? IEnumerator.Current => ((IEnumerator)_linkedListEnumerator).Current;

            public void Dispose()
            {
                _linkedListEnumerator.Dispose();
            }
        }

        private readonly struct KeyWrapper
        {
            public KeyWrapper(T value)
            {
                Value = value;
            }

            public T Value { get; }
        }

        private sealed class KeyWrapperEqualityComparer : IEqualityComparer<KeyWrapper>
        {
            private readonly IEqualityComparer<T> _equalityComparer;

            public KeyWrapperEqualityComparer(IEqualityComparer<T> equalityComparer)
            {
                _equalityComparer = equalityComparer;
            }

            public bool Equals(KeyWrapper x, KeyWrapper y)
            {
                if (x.Value is null)
                    return y.Value is null;         

                if (y.Value is null)
                    return false;

                return _equalityComparer.Equals(x.Value, y.Value);
            }

            public int GetHashCode(KeyWrapper obj)
            {
                if (obj.Value is null)
                    return 0;

                return _equalityComparer.GetHashCode(obj.Value);
            }
        }
    }
}
