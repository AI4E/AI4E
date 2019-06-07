using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AI4E.Storage.Projection
{
    public sealed class WrapperCollection<T, TWrapped> : ICollection<T>
    {
        private readonly ICollection<TWrapped> _wrappedCollection;
        private readonly Func<T, TWrapped> _unwrap;
        private readonly Func<TWrapped, T> _wrap;

        public WrapperCollection(
            ICollection<TWrapped> wrappedCollection,
            Func<T, TWrapped> unwrap,
            Func<TWrapped, T> wrap)
        {
            if (wrappedCollection is null)
                throw new ArgumentNullException(nameof(wrappedCollection));

            if (unwrap is null)
                throw new ArgumentNullException(nameof(unwrap));

            if (wrap is null)
                throw new ArgumentNullException(nameof(wrap));

            _wrappedCollection = wrappedCollection;
            _unwrap = unwrap;
            _wrap = wrap;
        }

        /// <inheritdoc />
        public void Add(T item)
        {
            _wrappedCollection.Add(_unwrap(item));
        }

        /// <inheritdoc />
        public void Clear()
        {
            _wrappedCollection.Clear();
        }

        /// <inheritdoc />
        public bool Contains(T item)
        {
            return _wrappedCollection.Contains(_unwrap(item));
        }

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (array.Length - arrayIndex < Count)
                throw new ArgumentException(
                    "The number of elements in the collection is greater than the " +
                    "available space from arrayIndex to the end of the destination array.");

            foreach (var item in _wrappedCollection)
            {
                array[arrayIndex++] = _wrap(item);
            }
        }

        /// <inheritdoc />
        public bool Remove(T item)
        {
            return _wrappedCollection.Remove(_unwrap(item));
        }

        /// <inheritdoc />
        public int Count => _wrappedCollection.Count;

        /// <inheritdoc />
        public bool IsReadOnly => _wrappedCollection.IsReadOnly;

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            return _wrappedCollection.Select(p => _wrap(p)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var item in (IEnumerable)_wrappedCollection)
            {
                yield return _wrap((TWrapped)item);
            }
        }
    }
}
