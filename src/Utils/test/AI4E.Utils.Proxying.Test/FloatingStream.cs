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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace AI4E.Utils.Proxying.Test
{
    public sealed class FloatingStream : Stream
    {
        private readonly AsyncProducerConsumerQueue<ArraySegment<byte>> _queue = new AsyncProducerConsumerQueue<ArraySegment<byte>>();
        private readonly CancellationTokenSource _disposedCancellationSource = new CancellationTokenSource();
        private ArraySegment<byte> _current = new ArraySegment<byte>();

        public FloatingStream() { }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count == 0)
                return;

            if (buffer.Length - offset < count)
                throw new ArgumentException(); // TODO

            if (_disposedCancellationSource.IsCancellationRequested)
                throw new ObjectDisposedException(GetType().FullName);

            _queue.Enqueue(new ArraySegment<byte>(buffer, offset, count));
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (buffer.Length - offset < count)
                throw new ArgumentException(); // TODO

            if (_current.Array == null || _current.Count == 0)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposedCancellationSource.Token);

                try
                {
                    _current = await _queue.DequeueAsync(cts.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            Debug.Assert(_current.Array != null);
            Debug.Assert(_current.Count > 0);

            var bytesToCopy = Math.Min(count, _current.Count);

            Array.Copy(_current.Array, _current.Offset, buffer, offset, bytesToCopy);

            if (_current.Count == bytesToCopy)
            {
                _current = default;
            }
            else
            {
                _current = new ArraySegment<byte>(_current.Array, _current.Offset + bytesToCopy, _current.Count - bytesToCopy);
            }

            return bytesToCopy;
        }

        protected override void Dispose(bool disposing)
        {
            _disposedCancellationSource.Cancel();
        }
    }
}
