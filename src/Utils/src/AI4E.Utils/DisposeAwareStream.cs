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
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;

namespace AI4E.Utils
{
    public sealed class DisposeAwareStream : Stream, IAsyncDisposable
    {
#pragma warning disable IDE0069, CA2213
        private readonly Stream _underlyingStream;
#pragma warning restore IDE0069, CA2213
        private readonly Func<Task> _disposeOperation;
        private readonly ILogger<DisposeAwareStream>? _logger;
        private readonly AsyncDisposeHelper _disposeHelper;

        public DisposeAwareStream(Stream underlyingStream,
                                  Func<Task> disposeOperation,
                                  ILogger<DisposeAwareStream>? logger = null)
        {
            if (underlyingStream == null)
                throw new ArgumentNullException(nameof(underlyingStream));

            if (disposeOperation == null)
                throw new ArgumentNullException(nameof(disposeOperation));

            _underlyingStream = underlyingStream;
            _disposeOperation = disposeOperation;
            _logger = logger;

            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #region Looped through ops

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _underlyingStream.FlushAsync(cancellationToken);
        }

        public override void Flush()
        {
            _underlyingStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _underlyingStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _underlyingStream.SetLength(value);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return _underlyingStream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override bool CanRead => _underlyingStream.CanRead;

        public override bool CanSeek => _underlyingStream.CanSeek;

        public override bool CanWrite => _underlyingStream.CanWrite;

        public override long Length => _underlyingStream.Length;

        public override long Position
        {
            get => _underlyingStream.Position;
            set => _underlyingStream.Position = value;
        }

        #endregion

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _underlyingStream
                    .ReadAsync(buffer, offset, count, cancellationToken)
                    .ConfigureAwait(false);

                if (result == 0)
                {
                    await _disposeHelper
                        .DisposeAsync()
                        .HandleExceptionsAsync(_logger)
                        .ConfigureAwait(false);
                }

                return result;
            }
            catch (Exception exc) when (exc is SocketException || exc is IOException || exc is ObjectDisposedException)
            {
                await _disposeHelper
                    .DisposeAsync()
                    .HandleExceptionsAsync(_logger)
                    .ConfigureAwait(false);

                throw;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                var result = _underlyingStream.Read(buffer, offset, count);

                if (result == 0)
                {
                    ExceptionHelper.HandleExceptions(() => _disposeHelper.Dispose(), _logger);
                }

                return result;
            }
            catch (Exception exc) when (exc is SocketException || exc is IOException || exc is ObjectDisposedException)
            {
                ExceptionHelper.HandleExceptions(() => _disposeHelper.Dispose(), _logger);
                throw;
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                await _underlyingStream
                    .WriteAsync(buffer, offset, count, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exc) when (exc is SocketException || exc is IOException || exc is ObjectDisposedException)
            {
                await _disposeHelper
                    .DisposeAsync()
                    .HandleExceptionsAsync(_logger)
                    .ConfigureAwait(false);
                throw;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                _underlyingStream.Write(buffer, offset, count);
            }
            catch (Exception exc) when (exc is SocketException || exc is IOException || exc is ObjectDisposedException)
            {
                ExceptionHelper.HandleExceptions(() => _disposeHelper.Dispose(), _logger);

                throw;
            }
        }

        #region Disposal

        [Obsolete("Call DiposeAsync() to get a task representing the object's disposal.")]
        public Task Disposal => _disposeHelper.Disposal;

        /// <inheritdoc/>
#if SUPPORTS_ASYNC_DISPOSABLE
        public override ValueTask DisposeAsync()
#else
        public ValueTask DisposeAsync()
#endif
        {
            return _disposeHelper.DisposeAsync();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposeHelper.Dispose();
            }
        }

        private async Task DisposeInternalAsync()
        {
            try
            {
#if SUPPORTS_ASYNC_DISPOSABLE
                await _underlyingStream
                    .DisposeAsync()
                    .HandleExceptionsAsync(_logger)
                    .ConfigureAwait(false);
#else
                ExceptionHelper.HandleExceptions(() =>_underlyingStream.Dispose(), _logger);
#endif
            }
            finally
            {
                await _disposeOperation()
                    .HandleExceptionsAsync(_logger)
                    .ConfigureAwait(false);
            }
        }

        #endregion
    }
}
