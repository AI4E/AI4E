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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;

namespace AI4E.Remoting
{
    public sealed partial class TcpEndPoint
    {
        private sealed class RemoteConnection : IAsyncDisposable
        {
            private readonly AsyncProcess _receiveProcess;
            private readonly Stream _stream;

            public RemoteConnection(RemoteEndPoint remoteEndPoint, Stream stream)
            {
                RemoteEndPoint = remoteEndPoint;
                _stream = stream;

                _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);
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
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
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
                    await message.ReadAsync(_stream, cancellation);
                }
                catch (IOException)
                {
                    _ = DisposeAsync();
                    throw;
                }

                return message;
            }

            #endregion

            public async ValueTask SendAsync(IMessage message, CancellationToken cancellation)
            {
                try
                {
                    await message.WriteAsync(_stream, cancellation);
                }
                catch (IOException)
                {
                    _ = DisposeAsync();
                    throw new OperationCanceledException();
                }
            }

            private ValueTask ReceiveAsync(IMessage message, CancellationToken cancellation)
            {
                return RemoteEndPoint.ReceiveAsync(message, cancellation);
            }

            public ValueTask DisposeAsync()
            {
                ValueTask result;
                try
                {
                    lock (_statusMutex)
                    {
                        _status = ConnectionStatus.Unconnected;
                    }
                }
                finally
                {
                    result = _receiveProcess.TerminateAsync().AsValueTask();
                }

                return result;
            }
        }

        private enum ConnectionStatus
        {
            Unconnected,
            Connected
        }
    }
}
