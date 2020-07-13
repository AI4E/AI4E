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

using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    /// <summary>
    /// Contains extension methods for the <see cref="Stream"/> class.
    /// </summary>
    public static class AI4EUtilsMemoryStreamExtensions
    {
        /// <summary>
        /// Asynchronously reads exactly the number of bytes from the stream that the specified buffer is long.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read from.</param>
        /// <param name="buffer">The memory of bytes that shall be filled with bytes read from the stream.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <c>null</c>.</exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown if the end of the stream was reached before completely filling <paramref name="buffer"/>.
        /// </exception>
        public static async ValueTask ReadExactAsync(
            this Stream stream, Memory<byte> buffer, CancellationToken cancellation = default)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            while (buffer.Length > 0)
            {
#pragma warning disable CA1062
                var readBytes = await stream.ReadAsync(buffer, cancellation);
#pragma warning restore CA1062

                if (readBytes == 0)
                    throw new EndOfStreamException();

                buffer = buffer.Slice(readBytes);
            }
        }

        /// <summary>
        /// Reads exactly the number of bytes from the stream that the specified buffer is long.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read from.</param>
        /// <param name="buffer">The span of bytes that shall be filled with bytes read from the stream.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <c>null</c>.</exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown if the end of the stream was reached before completely filling <paramref name="buffer"/>.
        /// </exception>
        public static void ReadExact(this Stream stream, Span<byte> buffer)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            while (buffer.Length > 0)
            {
#pragma warning disable CA1062
                var readBytes = stream.Read(buffer);
#pragma warning restore CA1062

                if (readBytes == 0)
                    throw new EndOfStreamException();

                buffer = buffer.Slice(readBytes);
            }
        }

        /// <summary>
        /// Asynchronously reads a single byte from the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read from.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the read byte.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <c>null</c>.</exception>
        /// <exception cref="EndOfStreamException"> Thrown if the end of the stream was reached.</exception>
        public static async ValueTask<byte> ReadByteExactAsync(
            this Stream stream, CancellationToken cancellation = default)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            using var bufferOwner = MemoryPool<byte>.Shared.RentExact(length: 1);
            var buffer = bufferOwner.Memory;

            await stream.ReadExactAsync(buffer, cancellation);
            return buffer.Span[0];
        }

        /// <summary>
        /// Reads a single byte from the stream.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read from.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>The read byte.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <c>null</c>.</exception>
        /// <exception cref="EndOfStreamException"> Thrown if the end of the stream was reached.</exception>
        public static byte ReadByteExact(this Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            Span<byte> buffer = stackalloc byte[1];
            stream.ReadExact(buffer);
            return buffer[0];
        }

        /// <summary>
        /// Asynchronously seeks forward the specified number of bytes from the current stream position.
        /// </summary>
        /// <param name="stream">The stream to seek.</param>
        /// <param name="offset">The number of bytes to seek foreward.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> id negative.</exception>
        /// <exception cref="EndOfStreamException"> Thrown if the end of the stream was reached.</exception>
        /// <remarks>
        /// Other then <see cref="Stream.Seek(long, SeekOrigin)"/> the stream does not need to be seekable.
        /// Seeking beyond the end of the stream is not support and results in a <see cref="EndOfStreamException"/> to be thrown.
        /// Seeking backwards is not supported.
        /// </remarks>
        public static async ValueTask SeekToPositionAsync(
            this Stream stream, long offset, CancellationToken cancellation = default)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (offset == 0)
                return;

#pragma warning disable CA1062 
            if (stream.CanSeek)
#pragma warning restore CA1062
            {
                if (stream.Position > stream.Length - offset)
                    throw new EndOfStreamException();

                stream.Position += offset;
            }
            else
            {
                using var memoryOwner = MemoryPool<byte>.Shared.Rent(-1);
                var memory = memoryOwner.Memory;
                while (offset > 0)
                {
                    var bufferLength = Math.Min(offset, memory.Length);
                    Debug.Assert(bufferLength <= int.MinValue);
                    var buffer = memory.Slice(0, unchecked((int)bufferLength));

                    var bytesRead = await stream.ReadAsync(buffer, cancellation);
                    if (bytesRead == 0)
                        throw new EndOfStreamException();
                    offset -= bytesRead;
                    Debug.Assert(offset >= 0);
                }
            }
        }

        /// <summary>
        /// Seeks forward the specified number of bytes from the current stream position.
        /// </summary>
        /// <param name="stream">The stream to seek.</param>
        /// <param name="offset">The number of bytes to seek forward.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> id negative.</exception>
        /// <exception cref="EndOfStreamException"> Thrown if the end of the stream was reached.</exception>
        /// <remarks>
        /// Other then <see cref="Stream.Seek(long, SeekOrigin)"/> the stream does not need to be seekable.
        /// Seeking beyond the end of the stream is not support and results in a <see cref="EndOfStreamException"/> to be thrown.
        /// Seeking backwards is not supported.
        /// </remarks>
        public static void SeekToPosition(this Stream stream, long offset)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (offset == 0)
                return;

#pragma warning disable CA1062 
            if (stream.CanSeek)
#pragma warning restore CA1062
            {
                if (stream.Position > stream.Length - offset)
                    throw new EndOfStreamException();

                stream.Position += offset;
            }
            else
            {
                Span<byte> span = stackalloc byte[1024];

                while (offset > 0)
                {
                    var bufferLength = Math.Min(offset, span.Length);
                    Debug.Assert(bufferLength <= int.MinValue);
                    var buffer = span.Slice(0, unchecked((int)bufferLength));

                    var bytesRead = stream.Read(buffer);
                    if (bytesRead == 0)
                        throw new EndOfStreamException();
                    offset -= bytesRead;
                    Debug.Assert(offset >= 0);
                }
            }
        }
    }
}
