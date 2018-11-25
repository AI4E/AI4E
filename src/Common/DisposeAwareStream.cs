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
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Internal
{
    internal sealed class DisposeAwareStream : Stream, IAsyncDisposable
    {
        private readonly NetworkStream _underlyingStream;
        private readonly Func<Task> _disposeOperation;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<DisposeAwareStream> _logger;
        private readonly AsyncDisposeHelper2 _disposeHelper;
        private readonly AsyncLock _writeLock = new AsyncLock();
        private DateTime _lastWriteTime;
        private static readonly byte[] _emptyBytes = new byte[0];

        public DisposeAwareStream(NetworkStream underlyingStream,
                                  IDateTimeProvider dateTimeProvider,
                                  Func<Task> disposeOperation,
                                  ILogger<DisposeAwareStream> logger)
        {
            if (underlyingStream == null)
                throw new ArgumentNullException(nameof(underlyingStream));

            if (disposeOperation == null)
                throw new ArgumentNullException(nameof(disposeOperation));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _underlyingStream = underlyingStream;
            _disposeOperation = disposeOperation;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;

            _lastWriteTime = _dateTimeProvider.GetCurrentTime();
            _disposeHelper = new AsyncDisposeHelper2(DisposeInternalAsync);
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
                var result = await _underlyingStream.ReadAsync(buffer, offset, count, cancellationToken);

                if (result == 0)
                {
                    await _disposeHelper.DisposeAsync().HandleExceptionsAsync(_logger);
                }

                return result;
            }
            catch (Exception exc) when (exc is SocketException || exc is IOException || exc is ObjectDisposedException)
            {
                await _disposeHelper.DisposeAsync().HandleExceptionsAsync(_logger);
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
                await _underlyingStream.WriteAsync(buffer, offset, count, cancellationToken);
            }
            catch (Exception exc) when (exc is SocketException || exc is IOException || exc is ObjectDisposedException)
            {
                await _disposeHelper.DisposeAsync().HandleExceptionsAsync(_logger);
                throw;
            }

            var now = _dateTimeProvider.GetCurrentTime();
            if (_lastWriteTime < now)
            {
                _lastWriteTime = now;
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

        public Task Disposal => _disposeHelper.Disposal;

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposeHelper.Dispose();
            }
        }

        private async Task DisposeInternalAsync()
        {
            ExceptionHelper.HandleExceptions(() => _underlyingStream.Close(), _logger);
            await _disposeOperation().HandleExceptionsAsync(_logger);
        }

        #endregion
    }
}
