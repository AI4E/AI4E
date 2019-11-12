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
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Memory.Compatibility;
using static System.Diagnostics.Debug;

namespace System.IO
{
    public static class AI4EUtilsMemoryCompatibilityStreamExtensions
    {
        private static readonly Func<Stream, Memory<byte>, CancellationToken, ValueTask<int>>? _readAsyncShim;
        private static readonly Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _writeAsyncShim;

        private static readonly ReadShim? _readShim;
        private static readonly WriteShim? _writeShim;

        private delegate int ReadShim(Stream stream, Span<byte> buffer);
        private delegate void WriteShim(Stream stream, ReadOnlySpan<byte> buffer);

#pragma warning disable CA1810
        static AI4EUtilsMemoryCompatibilityStreamExtensions()
#pragma warning restore CA1810
        {
            // Mono seems to define the methods but throws a NotImplementedException when called.
            // https://github.com/mono/mono/blob/c5b88ec4f323f2bdb7c7d0a595ece28dae66579c/mcs/class/corlib/corert/Stream.cs
            if (!RuntimeHelper.IsRunningOnMono())
            {
                var streamType = typeof(Stream);
                var readAsyncMethod = streamType.GetMethod(nameof(Stream.ReadAsync), new[] { typeof(Memory<byte>), typeof(CancellationToken) });

                if (readAsyncMethod != null)
                {
                    Assert(readAsyncMethod.ReturnType == typeof(ValueTask<int>));

                    var streamParameter = Expression.Parameter(typeof(Stream), "stream");
                    var bufferParameter = Expression.Parameter(typeof(Memory<byte>), "buffer");
                    var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
                    var methodCall = Expression.Call(streamParameter, readAsyncMethod, bufferParameter, cancellationTokenParameter);
                    _readAsyncShim = Expression.Lambda<Func<Stream, Memory<byte>, CancellationToken, ValueTask<int>>>(
                        methodCall,
                        streamParameter,
                        bufferParameter,
                        cancellationTokenParameter).Compile();
                }

                var writeAsyncMethod = streamType.GetMethod(nameof(Stream.WriteAsync), new[] { typeof(ReadOnlyMemory<byte>), typeof(CancellationToken) });

                if (writeAsyncMethod != null)
                {
                    Assert(writeAsyncMethod.ReturnType == typeof(ValueTask));

                    var streamParameter = Expression.Parameter(typeof(Stream), "stream");
                    var bufferParameter = Expression.Parameter(typeof(ReadOnlyMemory<byte>), "buffer");
                    var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
                    var methodCall = Expression.Call(streamParameter, writeAsyncMethod, bufferParameter, cancellationTokenParameter);
                    _writeAsyncShim = Expression.Lambda<Func<Stream, ReadOnlyMemory<byte>, CancellationToken, ValueTask>>(
                        methodCall,
                        streamParameter,
                        bufferParameter,
                        cancellationTokenParameter).Compile();
                }

                var readMethod = streamType.GetMethod(nameof(Stream.Read), new[] { typeof(Span<byte>) });

                if (readMethod != null)
                {
                    Assert(readMethod.ReturnType == typeof(int));
                    var streamParameter = Expression.Parameter(typeof(Stream), "stream");
                    var bufferParameter = Expression.Parameter(typeof(Span<byte>), "buffer");
                    var methodCall = Expression.Call(streamParameter, readMethod, bufferParameter);
                    _readShim = Expression.Lambda<ReadShim>(
                        methodCall,
                        streamParameter,
                        bufferParameter).Compile();
                }

                var writeMethod = streamType.GetMethod(nameof(Stream.Write), new[] { typeof(ReadOnlySpan<byte>) });

                if (writeMethod != null)
                {
                    Assert(writeMethod.ReturnType == typeof(void));
                    var streamParameter = Expression.Parameter(typeof(Stream), "stream");
                    var bufferParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "buffer");
                    var methodCall = Expression.Call(streamParameter, writeMethod, bufferParameter);
                    _writeShim = Expression.Lambda<WriteShim>(
                       methodCall,
                       streamParameter,
                       bufferParameter).Compile();
                }
            }
        }

        public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (_readAsyncShim != null)
            {
                return _readAsyncShim(stream, buffer, cancellationToken);
            }

            if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var memoryStreamBuffer))
            {
                var position = checked((int)stream.Position);
                var length = checked((int)stream.Length);
                var result = Math.Min(length - position, buffer.Length);

                memoryStreamBuffer.AsMemory().Slice(start: position, length: result).CopyTo(buffer);

                return new ValueTask<int>(result);
            }

            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var arraySegment))
            {
                return new ValueTask<int>(stream.ReadAsync(arraySegment.Array, arraySegment.Offset, arraySegment.Count));
            }

            return ReadCoreAsync(stream, buffer, cancellationToken);
        }

        private static async ValueTask<int> ReadCoreAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                var result = await stream.ReadAsync(array, offset: 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (result > 0)
                {
                    array.AsMemory().Slice(start: 0, length: result).CopyTo(buffer);
                }
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (_writeAsyncShim != null)
            {
                return _writeAsyncShim(stream, buffer, cancellationToken);
            }

            if (stream is MemoryStream memoryStream && memoryStream.CanWrite && memoryStream.TryGetBuffer(out var memoryStreamBuffer))
            {
                var position = checked((int)stream.Position);
                var length = checked((int)stream.Length);

                // Check if there is enough space in the stream.
                if (length - position >= buffer.Length)
                {
                    buffer.CopyTo(memoryStreamBuffer.AsMemory().Slice(start: position));
                    return new ValueTask(Task.CompletedTask); // TODO: How can we return an already completed ValueTask??
                }
            }

            if (MemoryMarshal.TryGetArray(buffer, out var arraySegment))
            {
                return new ValueTask(stream.WriteAsync(arraySegment.Array, arraySegment.Offset, arraySegment.Count));
            }

            return WriteCoreAsync(stream, buffer, cancellationToken);
        }

        private static async ValueTask WriteCoreAsync(Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                buffer.CopyTo(array);

                await stream.WriteAsync(array, offset: 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        public static int Read(this Stream stream, Span<byte> buffer)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (_readShim != null)
            {
                return _readShim(stream, buffer);
            }

            if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var memoryStreamBuffer))
            {
                var position = checked((int)stream.Position);
                var length = checked((int)stream.Length);
                var result = Math.Min(length - position, buffer.Length);

                memoryStreamBuffer.AsSpan().Slice(start: position, length: result).CopyTo(buffer);

                return result;
            }

            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                var result = stream.Read(array, offset: 0, buffer.Length);
                if (result > 0)
                {
                    array.AsSpan().Slice(start: 0, length: result).CopyTo(buffer);
                }
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        public static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (_writeShim != null)
            {
                _writeShim(stream, buffer);
                return;
            }

            if (stream is MemoryStream memoryStream && memoryStream.CanWrite && memoryStream.TryGetBuffer(out var memoryStreamBuffer))
            {
                var position = checked((int)stream.Position);
                var length = checked((int)stream.Length);

                // Check if there is enough space in the stream.
                if (length - position >= buffer.Length)
                {
                    buffer.CopyTo(memoryStreamBuffer.AsSpan().Slice(start: position));
                    return;
                }
            }

            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                buffer.CopyTo(array);

                stream.Write(array, offset: 0, buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }
}
