/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

namespace AI4E.ApplicationParts
{
    internal sealed class ApplicationPartsList : IList<ApplicationPart>
    {
        private volatile ImmutableList<ApplicationPart> _inner = ImmutableList<ApplicationPart>.Empty;

        public int IndexOf(ApplicationPart item)
        {
            return _inner.IndexOf(item);
        }

        public void Insert(int index, ApplicationPart item)
        {
            ImmutableList<ApplicationPart> current = _inner, start, desired;

            do
            {
                start = current;
                desired = start.Insert(index, item);
                current = Interlocked.CompareExchange(ref _inner, desired, start);
            }
            while (start != current);

            OnCollectionChanged();
        }

        public void RemoveAt(int index)
        {
            ImmutableList<ApplicationPart> current = _inner, start, desired;

            do
            {
                start = current;
                desired = start.RemoveAt(index);
                current = Interlocked.CompareExchange(ref _inner, desired, start);
            }
            while (start != current);

            OnCollectionChanged();
        }

        public ApplicationPart this[int index]
        {
            get => _inner[index];
            set
            {
                ImmutableList<ApplicationPart> current = _inner, start, desired;

                do
                {
                    start = current;
                    desired = start.SetItem(index, value);
                    current = Interlocked.CompareExchange(ref _inner, desired, start);
                }
                while (start != current);

                OnCollectionChanged();
            }
        }

        public void Add(ApplicationPart item)
        {
            ImmutableList<ApplicationPart> current = _inner, start, desired;

            do
            {
                start = current;
                desired = start.Add(item);
                current = Interlocked.CompareExchange(ref _inner, desired, start);
            }
            while (start != current);

            OnCollectionChanged();
        }

        public void Clear()
        {
            var previous = Interlocked.Exchange(ref _inner, ImmutableList<ApplicationPart>.Empty);

            if (previous.Any())
            {
                OnCollectionChanged();
            }
        }

        public bool Contains(ApplicationPart item)
        {
            return _inner.Contains(item);
        }

        public void CopyTo(ApplicationPart[] array, int arrayIndex)
        {
            _inner.CopyTo(array, arrayIndex);
        }

        public bool Remove(ApplicationPart item)
        {
            ImmutableList<ApplicationPart> current = _inner, start, desired;

            do
            {
                start = current;

                desired = start.Remove(item);

                if (desired == start)
                    return false;

                current = Interlocked.CompareExchange(ref _inner, desired, start);
            }
            while (start != current);

            OnCollectionChanged();

            return true;
        }

        public int Count => _inner.Count;

        public bool IsReadOnly => false;

        public IEnumerator<ApplicationPart> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        public event System.EventHandler CollectionChanged;

        private void OnCollectionChanged()
        {
            CollectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
