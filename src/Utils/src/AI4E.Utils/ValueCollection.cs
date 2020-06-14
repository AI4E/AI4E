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
using System.Linq;

namespace AI4E.Utils
{
    /// <summary>
    /// A wrapper for collections that ensures that the underlying collection does not contain null entries.
    /// </summary>
    /// <typeparam name="T"> The type of the elements in the collection.</typeparam>
    public sealed class ValueCollection<T> : ICollection<T>
        where T : notnull
    {
        private readonly ICollection<T> _underlyingCollection;

        /// <summary>
        /// Creates a new instance of the <see cref="ValueCollection{T}"/> class backed by a <see cref="List{T}"/>.
        /// </summary>
        public ValueCollection()
        {
            _underlyingCollection = new List<T>();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ValueCollection{T}"/> class backed by the specified collection.
        /// </summary>
        /// <param name="underlyingCollection">The backing collection.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="underlyingCollection"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if an item in <paramref name="underlyingCollection"/> is <c>null</c>.
        /// </exception>
        public ValueCollection(ICollection<T> underlyingCollection)
        {
            if (underlyingCollection is null)
                throw new ArgumentNullException(nameof(underlyingCollection));

            if (!typeof(T).IsValueType && underlyingCollection.Any(p => p is null))
            {
                throw new ArgumentException(
                    "The collection must not contain null entries.",
                    nameof(underlyingCollection));
            }

            _underlyingCollection = underlyingCollection;
        }

        /// <inheritdoc/>
        public int Count => _underlyingCollection.Count;

        bool ICollection<T>.IsReadOnly => _underlyingCollection.IsReadOnly;

        /// <inheritdoc/>
        public void Add(T item)
        {
            if (item is null)
                throw new ArgumentNullException(nameof(item));

            _underlyingCollection.Add(item);
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            return _underlyingCollection.Remove(item);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _underlyingCollection.Clear();
        }

        /// <inheritdoc/>
        public bool Contains(T item)
        {
            return _underlyingCollection.Contains(item);
        }

        /// <inheritdoc/>
        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            _underlyingCollection.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            return _underlyingCollection.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_underlyingCollection).GetEnumerator();
        }
    }
}
