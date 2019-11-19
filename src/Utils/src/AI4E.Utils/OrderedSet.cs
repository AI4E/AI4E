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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * https://gist.github.com/gmamaladze/3d60c127025c991a087e
 * This code is distributed under MIT license. Copyright (c) 2013 George Mamaladze
 *  See license.txt or http://opensource.org/licenses/mit-license.php
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AI4E.Utils
{
#pragma warning disable CA1710
    public class OrderedSet<T> : ICollection<T>, IReadOnlyCollection<T>, ISet<T>
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
            if (comparer is null)
                throw new ArgumentNullException(nameof(comparer));

            var keyComparer = new KeyWrapperEqualityComparer(comparer);

            _dictionary = new Dictionary<KeyWrapper, LinkedListNode<T>>(keyComparer);
            _linkedList = new LinkedList<T>();
        }

        public OrderedSet(IEnumerable<T> collection, IEqualityComparer<T> comparer) : this(comparer)
        {
            if (collection is null)
                throw new ArgumentNullException(nameof(collection));

            UnionWith(collection);
        }

        public OrderedSet(IEnumerable<T> collection) : this(EqualityComparer<T>.Default)
        {
            if (collection is null)
                throw new ArgumentNullException(nameof(collection));

            UnionWith(collection);
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

        /// <summary>
        /// Modifies the current set so that it contains all elements that are present in both the current set and in the
        /// specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public void UnionWith(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            foreach (var element in other)
            {
                Add(element);
            }
        }

        /// <summary>
        /// Modifies the current set so that it contains only elements that are also in a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public void IntersectWith(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            var otherSet = other as ISet<T> ?? new HashSet<T>(other);
            var current = _linkedList.First;

            while (current != null)
            {
                var previous = current;
                var removePrevious = !otherSet.Contains(current.Value);
                current = current.Next;

                if (removePrevious)
                {
                    _linkedList.Remove(previous);
                    _dictionary.Remove(new KeyWrapper(previous.Value));
                }
            }
        }

        /// <summary>
        /// Removes all elements in the specified collection from the current set.
        /// </summary>
        /// <param name="other">The collection of items to remove from the set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public void ExceptWith(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            foreach (var element in other)
            {
                Remove(element);
            }
        }

        /// <summary>
        /// Modifies the current set so that it contains only elements that are present either in the current set or in the
        /// specified collection, but not both.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            foreach (var element in other)
            {
                if (!Remove(element))
                {
                    Add(element);
                }
            }
        }

        /// <summary>
        /// Determines whether a set is a subset of a specified collection.
        /// </summary>
        /// <returns>
        /// true if the current set is a subset of <paramref name="other" />; otherwise, false.
        /// </returns>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            var otherHashset = other as ISet<T> ?? new HashSet<T>(other);
            return otherHashset.IsSupersetOf(this);
        }

        /// <summary>
        /// Determines whether the current set is a superset of a specified collection.
        /// </summary>
        /// <returns>
        /// true if the current set is a superset of <paramref name="other" />; otherwise, false.
        /// </returns>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public bool IsSupersetOf(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            return other.All(Contains);
        }

        /// <summary>
        /// Determines whether the current set is a correct superset of a specified collection.
        /// </summary>
        /// <returns>
        /// true if the <see cref="ISet{T}" /> object is a correct superset of
        /// <paramref name="other" />; otherwise, false.
        /// </returns>
        /// <param name="other">The collection to compare to the current set. </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            var otherHashset = other as ISet<T> ?? new HashSet<T>(other);
            return otherHashset.IsProperSubsetOf(this);
        }

        /// <summary>
        /// Determines whether the current set is a property (strict) subset of a specified collection.
        /// </summary>
        /// <returns>
        /// true if the current set is a correct subset of <paramref name="other" />; otherwise, false.
        /// </returns>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            var otherHashset = other as ISet<T> ?? new HashSet<T>(other);
            return otherHashset.IsProperSupersetOf(this);
        }

        /// <summary>
        /// Determines whether the current set overlaps with the specified collection.
        /// </summary>
        /// <returns>
        /// true if the current set and <paramref name="other" /> share at least one common element; otherwise, false.
        /// </returns>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public bool Overlaps(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            if (Count == 0)
                return false;

            return other.Any(Contains);
        }

        /// <summary>
        /// Determines whether the current set and the specified collection contain the same elements.
        /// </summary>
        /// <returns>
        /// true if the current set is equal to <paramref name="other" />; otherwise, false.
        /// </returns>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="other" /> is <c>null</c>.</exception>
        public bool SetEquals(IEnumerable<T> other)
        {
            if (other is null)
                throw new ArgumentNullException(nameof(other));

            var otherHashset = other as ISet<T> ?? new HashSet<T>(other);
            return otherHashset.SetEquals(this);
        }

        public struct Enumerator : IEnumerator<T>, IDisposable, IEnumerator
        {
            private LinkedList<T>.Enumerator _linkedListEnumerator;

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
