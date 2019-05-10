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
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;

namespace AI4E.Remoting
{
    public sealed partial class TcpEndPoint
    {
        internal sealed class RemoteConnection : IAsyncDisposable
        {
            private readonly AsyncDisposeHelper _asyncDisposeHelper;
            private readonly AsyncProcess _receiveProcess;

            public RemoteConnection(RemoteEndPoint remoteEndPoint, Stream stream)
            {
                RemoteEndPoint = remoteEndPoint;
                Stream = stream;

                _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
                _asyncDisposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
                _status = ConnectionStatus.Connected;
            }

            public ILogger Logger => LocalEndPoint._logger;
            public TcpEndPoint LocalEndPoint => RemoteEndPoint.LocalEndPoint;
            public RemoteEndPoint RemoteEndPoint { get; }

            private ConnectionStatus _status = ConnectionStatus.Unconnected;
            private readonly object _statusMutex = new object();

            public ConnectionStatus Status
            {
                get
                {
                    lock (_statusMutex)
                    {
                        return _status;
                    }
                }
            }

            // For test purposes only
            internal Stream Stream { get; }

            #region ReceiveProcess

            private async Task ReceiveProcess(CancellationToken cancellation)
            {
                Logger?.LogDebug($"Started receive process for remote address {RemoteEndPoint.RemoteAddress} on local address {LocalEndPoint.LocalAddress}.");

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        var message = await ReceiveAsync(cancellation);
                        _ = Task.Run(() => ReceiveAsync(message, cancellation).HandleExceptions());
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception exc)
                    {
                        Logger?.LogWarning(exc,
                            $"Failure on message receive from remote address {RemoteEndPoint.RemoteAddress} on local address {LocalEndPoint.LocalAddress}.");
                    }
                }
            }

            private async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
            {
                var message = new Message();

                try
                {
                    // The underlying network stream does not cancel the operation when we ask it for,
                    // but we can just leave alone the operation, as we get cancelled only, if we dispose
                    // so the next operation, we do is closing the underlying socket which will cancel the
                    // operation anyway.
                    await message.ReadAsync(Stream, cancellation).WithCancellation(cancellation);
                }
                catch (ObjectDisposedException)
                {
                    Dispose();
                    _receiveProcess.Terminate();
                    Debug.Assert(cancellation.IsCancellationRequested);
                    throw new OperationCanceledException();
                }
                catch (IOException)
                {
                    Dispose();
                    _receiveProcess.Terminate();
                    Debug.Assert(cancellation.IsCancellationRequested);
                    throw new OperationCanceledException();
                }

                return message;
            }

            #endregion

            public async ValueTask SendAsync(IMessage message, CancellationToken cancellation)
            {
                if (_asyncDisposeHelper.IsDisposed)
                    throw new OperationCanceledException();

                try
                {
                    await message.WriteAsync(Stream, cancellation);
                }
                catch (ObjectDisposedException)
                {
                    Dispose();
                    throw new OperationCanceledException();
                }
                catch (IOException)
                {
                    Dispose();
                    throw new OperationCanceledException();
                }
            }

            private ValueTask ReceiveAsync(IMessage message, CancellationToken cancellation)
            {
                return RemoteEndPoint.ReceiveAsync(message, cancellation);
            }

            #region Disposal

            private async ValueTask DisposeInternalAsync()
            {
#if DEBUG
                try
                {
#endif
                    try
                    {
                        try
                        {
                            try
                            {
                                lock (_statusMutex)
                                {
                                    _status = ConnectionStatus.Unconnected;
                                }
                            }
                            finally
                            {
                                await _receiveProcess.TerminateAsync();
                            }
                        }
                        finally
                        {
                            Stream.Close();
                        }
                    }
                    finally
                    {
                        RemoteEndPoint.Reconnect();
                    }
#if DEBUG
                }
                catch (Exception exc)
                {
                    Debugger.Break();
                    Debug.Fail(exc.Message);
                }
#endif
            }

            public ValueTask DisposeAsync()
            {
                return _asyncDisposeHelper.DisposeAsync();
            }

            public void Dispose()
            {
                _asyncDisposeHelper.Dispose();
            }

            #endregion
        }

        internal enum ConnectionStatus
        {
            Unconnected,
            Connected
        }
    }
}
