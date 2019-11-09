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

#pragma warning disable CA1815

using System;
using static System.Diagnostics.Debug;

namespace AI4E.Utils.Memory
{
    public struct MemoryWriter<T>
    {
        private readonly Memory<T> _memory;
        private int _position;

        public MemoryWriter(Memory<T> memory)
        {
            _memory = memory;
            _position = 0;
        }

        public void Append(ReadOnlySpan<T> span)
        {
            var spanX = _memory.Span;

            Assert(_position + span.Length <= _memory.Length);
            span.CopyTo(spanX.Slice(_position));
            _position += span.Length;
        }

        public void Append(T c)
        {
            var span = _memory.Span;

            Assert(_position + 1 <= _memory.Length);
            span[_position] = c;
            _position += 1;
        }

        public Memory<T> GetMemory()
        {
            return _memory.Slice(0, _position);
        }
    }
}

#pragma warning restore CA1815
