/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        ObjectPool.cs 
 * Types:           (1) AI4E.ObjectPool
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   04.06.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

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
using System.Collections.Concurrent;

namespace AI4E.Internal
{
    public class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> _objects = new ConcurrentBag<T>();
        private readonly Func<T> _objectGenerator;

        public ObjectPool(Func<T> objectGenerator)
        {
            if (objectGenerator == null)
                throw new ArgumentNullException(nameof(objectGenerator));

            _objectGenerator = objectGenerator;
        }

        public T GetObject()
        {
            if (_objects.TryTake(out var item))
                return item;

            return _objectGenerator();
        }

        public void PutObject(T item)
        {
            if (_objects.Count > 30)
                return;

            _objects.Add(item);
        }
    }
}
