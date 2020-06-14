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
using System.Buffers;

namespace AI4E.Utils
{
#pragma warning disable CA1815
    public readonly struct SlicedMemoryOwner<T> : IMemoryOwner<T>
#pragma warning restore CA1815
    {
        private readonly IMemoryOwner<T>? _memoryOwner;
        private readonly int _start;
        private readonly int _length;

        public SlicedMemoryOwner(IMemoryOwner<T> memoryOwner, int start, int length)
        {
            if (memoryOwner is null)
                throw new ArgumentNullException(nameof(memoryOwner));

            if (memoryOwner is SlicedMemoryOwner<T> slicedMemoryOwner)
            {
                this = new SlicedMemoryOwner<T>(slicedMemoryOwner, start, length);
                return;
            }

            var memoryLenght = memoryOwner.Memory.Length;

            if (start < 0 || start > memoryLenght)
                throw new ArgumentOutOfRangeException(nameof(start));

            if (length > memoryLenght - start)
                throw new ArgumentOutOfRangeException(nameof(length));

            _memoryOwner = memoryOwner;
            _start = start;
            _length = length;
        }

        public SlicedMemoryOwner(IMemoryOwner<T> memoryOwner, int start)
        {
            if (memoryOwner is null)
                throw new ArgumentNullException(nameof(memoryOwner));

            if (memoryOwner is SlicedMemoryOwner<T> slicedMemoryOwner)
            {
                this = new SlicedMemoryOwner<T>(slicedMemoryOwner, start);
                return;
            }

            var memoryLenght = memoryOwner.Memory.Length;

            if (start < 0 || start > memoryLenght)
                throw new ArgumentOutOfRangeException(nameof(start));

            _memoryOwner = memoryOwner;
            _start = start;
            _length = memoryLenght - start;
        }

        public SlicedMemoryOwner(SlicedMemoryOwner<T> memoryOwner, int start, int length)
        {
            var memoryLenght = memoryOwner.Memory.Length;

            if (start < 0 || start > memoryLenght)
                throw new ArgumentOutOfRangeException(nameof(start));

            if (length > memoryLenght - start)
                throw new ArgumentOutOfRangeException(nameof(length));

            _memoryOwner = memoryOwner._memoryOwner;
            _start = start + memoryOwner._start;
            _length = length;
        }

        public SlicedMemoryOwner(SlicedMemoryOwner<T> memoryOwner, int start)
        {
            var memoryLenght = memoryOwner.Memory.Length;

            if (start < 0 || start > memoryLenght)
                throw new ArgumentOutOfRangeException(nameof(start));

            _memoryOwner = memoryOwner._memoryOwner;
            _start = start + memoryOwner._start;
            _length = memoryOwner._length;
        }

        public Memory<T> Memory => _memoryOwner?.Memory.Slice(_start, _length) ?? Memory<T>.Empty;

        public void Dispose()
        {
            _memoryOwner?.Dispose();
        }

        public SlicedMemoryOwner<T> Slice(int start)
        {
            return new SlicedMemoryOwner<T>(this, start);
        }

        public SlicedMemoryOwner<T> Slice(int start, int length)
        {
            return new SlicedMemoryOwner<T>(this, start, length);
        }
    }
}
