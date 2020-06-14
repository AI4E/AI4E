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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

#nullable disable

namespace AI4E.Internal
{
    internal abstract class ReconnectionManagerBase : IDisposable
    {
        private readonly ILogger _logger;

        // Caches the delegate
        private readonly Func<Task> _getConnectionLoseTask;

        private readonly AsyncManualResetEvent _connectionLost = new AsyncManualResetEvent(set: true);
        private readonly object _connectionLock = new object();
        private Task _connectionTask;
        private bool _isInitialConnection = true;

        private volatile CancellationTokenSource _disposalSource = new CancellationTokenSource();

        public ReconnectionManagerBase(ILogger logger = null)
        {
            _logger = logger;
            _getConnectionLoseTask = GetConnectionLoseTask;
        }

        public ConnectionLostToken ConnectionLost => new ConnectionLostToken(_getConnectionLoseTask);

        private Task GetConnectionLoseTask()
        {
            // Initial state, or the connection is broken and not yet re-established.
            var connectionLose = _connectionLost.WaitAsync();

            if (connectionLose.IsCompleted)
            {
                return Task.CompletedTask;
            }

            Task connectionTask;

            lock (_connectionLock)
            {
                connectionTask = _connectionTask;
            }

            // We are currently re-establishing the connection.
            if (connectionTask != null)
            {
                return Task.CompletedTask;
            }

            return connectionLose;
        }

        public void Reconnect()
        {
            _ = ReconnectInternalAsync();
        }

        public ValueTask ReconnectAsync(CancellationToken cancellation)
        {
            return ReconnectInternalAsync().WithCancellation(cancellation);
        }

        // https://github.com/StephenCleary/AsyncEx/issues/151
        private async ValueTask ReconnectInternalAsync()
        {
            bool isInitialConnection;
            lock (_connectionLock)
            {
                isInitialConnection = _isInitialConnection;
                _isInitialConnection = false;
            }

            var disposalSource = _disposalSource; // Volatile read op

            if (disposalSource == null)
            {
                // We are disposed.
                return;
            }

            _connectionLost.Set();

            async Task Reconnect()
            {
                await Task.Yield();
                try
                {
                    // Reconnect
                    await ReconnectCoreAsync(isInitialConnection, cancellation: disposalSource.Token);
                    isInitialConnection = false;
                }
                finally
                {
                    lock (_connectionLock)
                    {
                        _connectionTask = null;
                    }
                }
            }

            await OnConnectionEstablishing(disposalSource.Token);

            Task connectionTask;
            while (_connectionLost.IsSet || isInitialConnection)
            {
                lock (_connectionLock)
                {
                    if (_connectionTask == null)
                        _connectionTask = Reconnect();

                    connectionTask = _connectionTask;
                }

                await connectionTask;
            }

            await OnConnectionEstablished(disposalSource.Token);
        }

        protected virtual ValueTask OnConnectionEstablished(CancellationToken cancellation)
        {
            return default;
        }

        protected virtual ValueTask OnConnectionEstablishing(CancellationToken cancellation)
        {
            return default;
        }

        private readonly AsyncLock _establishConnectionLock = new AsyncLock();

        protected abstract ValueTask EstablishConnectionAsync(bool isInitialConnection, CancellationToken cancellation);

        private async ValueTask ReconnectCoreAsync(bool isInitialConnection, CancellationToken cancellation)
        {
            // We are waiting one second after the first failed attempt to connect.
            // For each failed attempt, we increase the waited time to the next connection attempt,
            // until we reach an upper limit of 12 seconds.
            var timeToWait = new TimeSpan(1000 * TimeSpan.TicksPerMillisecond);
            var timeToWaitMax = new TimeSpan(12000 * TimeSpan.TicksPerMillisecond);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    // We will re-establish the underlying connection now. => Reset the connection lost indicator.
                    _connectionLost.Reset();

                    using (await _establishConnectionLock.LockAsync(cancellation))
                    {
                        await EstablishConnectionAsync(isInitialConnection, cancellation);
                    }

                    // The underlying connection was not lost in the meantime.
                    if (!_connectionLost.IsSet)
                    {
                        break;
                    }
                }
                catch (ObjectDisposedException) { throw; }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    Console.WriteLine("Error in ecc: " + exc.ToString()); // TODO: Log
                    _logger?.LogWarning($"Reconnection failed. Trying again in {timeToWait.TotalSeconds} sec.");

                    await Task.Delay(timeToWait, cancellation);

                    if (timeToWait < timeToWaitMax)
                        timeToWait = new TimeSpan(timeToWait.Ticks * 2);
                }
            }
        }

        public void Dispose()
        {
            var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

            if (disposalSource != null)
            {
                using (disposalSource)
                {
                    disposalSource.Cancel();
                }
            }
        }

        internal readonly struct ConnectionLostToken
        {
            private readonly Func<Task> _connectionLose;

            internal ConnectionLostToken(Func<Task> connectionLose)
            {
                _connectionLose = connectionLose;
            }

            public bool IsConnectionLost => _connectionLose?.Invoke()?.IsCompleted ?? true;

            public Task AsTask()
            {
                return _connectionLose?.Invoke() ?? Task.CompletedTask;
            }

            public static implicit operator Task(ConnectionLostToken token)
            {
                return token.AsTask();
            }
        }
    }
}
