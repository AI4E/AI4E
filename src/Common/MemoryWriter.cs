using System;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    internal struct MemoryWriter<T>
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
