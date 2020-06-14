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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * BlazorSignalR (https://github.com/csnewman/BlazorSignalR)
 *
 * MIT License
 *
 * Copyright (c) 2018 csnewman
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace AI4E.AspNetCore.Blazor.SignalR
{
    public class BlazorWebSocketsTransport : IDuplexPipe
    {
        private IDuplexPipe? _application;
        private readonly ILogger? _logger;
        private readonly IJSRuntime _jsRuntime;
        private volatile bool _aborted;

        private IDuplexPipe? _transport;

#pragma warning disable CA1822
        public string InternalWebSocketId { [JSInvokable] get; }
        public string? WebSocketAccessToken { [JSInvokable] get; }
#pragma warning restore CA1822

        internal Task Running { get; private set; } = Task.CompletedTask;

        public PipeReader? Input => _transport?.Input;
        public PipeWriter? Output => _transport?.Output;

        private TaskCompletionSource<object?>? _startTask;
        private TaskCompletionSource<object?>? _receiveTask;

        public BlazorWebSocketsTransport(string? token, IJSRuntime jsRuntime, ILoggerFactory? loggerFactory)
        {
            if (jsRuntime == null)
                throw new ArgumentNullException(nameof(jsRuntime));

            _logger = loggerFactory?.CreateLogger<BlazorWebSocketsTransport>();
            InternalWebSocketId = Guid.NewGuid().ToString();
            WebSocketAccessToken = token;
            _jsRuntime = jsRuntime;
        }

        public async Task StartAsync(Uri url, TransferFormat transferFormat)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (transferFormat != TransferFormat.Binary && transferFormat != TransferFormat.Text)
            {
                throw new ArgumentException(
                    $"The '{transferFormat}' transfer format is not supported by this transport.",
                    nameof(transferFormat));
            }


            Log.StartTransport(_logger, transferFormat);

            // Create connection
            _startTask = new TaskCompletionSource<object?>();
            await _jsRuntime.InvokeAsync<object>(
                "BlazorSignalR.WebSocketsTransport.CreateConnection", url.ToString(),
                transferFormat == TransferFormat.Binary, DotNetObjectReference.Create(this));

            await _startTask.Task.ConfigureAwait(false);
            _startTask = null;

            Log.StartedTransport(_logger);

            // Create the pipe pair (Application's writer is connected to Transport's reader, and vice versa)
            var options = ClientPipeOptions.DefaultOptions;
            var pair = DuplexPipe.CreateConnectionPair(options, options);

            _transport = pair.Transport;
            _application = pair.Application;

            Running = ProcessSocketAsync();
        }

        private async Task ProcessSocketAsync()
        {
            // Begin sending and receiving.
            var receiving = StartReceiving();
            var sending = StartSending();

            // Wait for send or receive to complete
            var trigger = await Task.WhenAny(receiving, sending).ConfigureAwait(false);

            if (trigger == receiving)
            {
                // We're waiting for the application to finish and there are 2 things it could be doing
                // 1. Waiting for application data
                // 2. Waiting for a websocket send to complete

                // Cancel the application so that ReadAsync yields
                Debug.Assert(_application != null);
                _application!.Input.CancelPendingRead();

                using var delayCts = new CancellationTokenSource();
                var resultTask = await Task.WhenAny(
                    sending, Task.Delay(TimeSpan.FromSeconds(5.0), delayCts.Token)).ConfigureAwait(false);

                if (resultTask != sending)
                {
                    _aborted = true;

                    // Abort the websocket if we're stuck in a pending send to the client
                    Debug.Assert(_receiveTask != null);
                    _receiveTask!.SetCanceled();
                    await CloseWebSocketAsync().ConfigureAwait(false);
                }
                else
                {
                    // Cancel the timeout
                    delayCts.Cancel();
                }
            }
            else
            {
                // We're waiting on the websocket to close and there are 2 things it could be doing
                // 1. Waiting for websocket data
                // 2. Waiting on a flush to complete (backpressure being applied)

                _aborted = true;

                // Abort the websocket if we're stuck in a pending receive from the client
                Debug.Assert(_receiveTask != null);
                _receiveTask!.SetCanceled();
                await CloseWebSocketAsync().ConfigureAwait(false);

                // Cancel any pending flush so that we can quit
                Debug.Assert(_application != null);
                _application!.Output.CancelPendingFlush();
            }
        }

        private async Task StartReceiving()
        {
            try
            {
                var task = new TaskCompletionSource<object?>();
                _receiveTask = task;

                // Wait until js side stops
                await task.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log.ReceiveCanceled(_logger);
            }
            catch (Exception ex)
            {
                if (!_aborted)
                {
                    Debug.Assert(_application != null);
                    _application!.Output.Complete(ex);

                    // We re-throw here so we can communicate that there was an error when sending
                    // the close frame
                    throw;
                }
            }
            finally
            {
                // We're done writing
                Debug.Assert(_application != null);
                _application!.Output.Complete();

                Log.ReceiveStopped(_logger);
            }
        }

        [JSInvokable]
        public void HandleWebSocketMessage(string msg)
        {
            _logger?.LogDebug($"HandleWebSocketMessage \"{msg}\"");

            // Decode data
            var data = Convert.FromBase64String(msg);

            Log.MessageReceived(_logger, data.Length);

            if (_application is null || _receiveTask is null)
                return;

            // Write to stream
            var flushResult = _application.Output.WriteAsync(data).Result;

            // Handle cancel
            if (flushResult.IsCanceled || flushResult.IsCompleted)
            {
                _receiveTask.SetCanceled();
            }
        }

        private async Task StartSending()
        {
            Exception? error; // TODO: What is done with the error finally?

            try
            {
                while (true)
                {
                    Debug.Assert(_application != null);
                    var result = await _application!.Input.ReadAsync();
                    var buffer = result.Buffer;

                    // Get a frame from the application

                    try
                    {
                        if (result.IsCanceled)
                        {
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            try
                            {
                                Log.ReceivedFromApp(_logger, buffer.Length);

                                var data = Convert.ToBase64String(buffer.ToArray());

                                Log.SendStarted(_logger);

                                await _jsRuntime.InvokeAsync<object>(
                                    "BlazorSignalR.WebSocketsTransport.Send", data, DotNetObjectReference.Create(this));
                            }
#pragma warning disable CA1031
                            catch (Exception ex)
#pragma warning restore CA1031
                            {
                                if (!_aborted)
                                {
                                    Log.ErrorSendingMessage(_logger, ex);
                                }

                                break;
                            }
                        }
                        else if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        _application.Input.AdvanceTo(buffer.End);
                    }
                }
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                error = ex;
            }
            finally
            {
                await CloseWebSocketAsync().ConfigureAwait(false);

                Debug.Assert(_application != null);
                _application!.Input.Complete();

                Log.SendStopped(_logger);
            }
        }

        public async Task StopAsync()
        {
            Log.TransportStopping(_logger);

            if (_application == null)
            {
                // We never started
                return;
            }


            // Kill js side
            _startTask?.SetCanceled();
            _receiveTask?.SetCanceled();
            await CloseWebSocketAsync().ConfigureAwait(false);

            Debug.Assert(_transport != null);
            _transport!.Output.Complete();
            _transport!.Input.Complete();

            // Cancel any pending reads from the application, this should start the entire shutdown process
            _application.Input.CancelPendingRead();

            try
            {
                await Running.ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Log.TransportStopped(_logger, ex);
                // exceptions have been handled in the Running task continuation by closing the channel with the exception
                return;
            }

            Log.TransportStopped(_logger, null);
        }

        public async Task CloseWebSocketAsync()
        {
            Log.ClosingWebSocket(_logger);
            try
            {
                await _jsRuntime.InvokeAsync<object>(
                    "BlazorSignalR.WebSocketsTransport.CloseConnection", DotNetObjectReference.Create(this));
            }
#pragma warning disable CA1031
            catch (Exception e)
#pragma warning restore CA1031
            {
                Log.ClosingWebSocketFailed(_logger, e);
            }
        }

        [JSInvokable]
        public void HandleWebSocketError(string msg)
        {
            _logger?.LogDebug($"HandleWebSocketError \"{msg}\"");
            _startTask?.SetException(new Exception(msg));
            _receiveTask?.SetException(new Exception(msg));
        }

        [JSInvokable]
        public void HandleWebSocketOpened()
        {
            _logger?.LogDebug("HandleWebSocketOpened");
            _startTask?.SetResult(null);
        }

        [JSInvokable]
        public void HandleWebSocketClosed()
        {
            _logger?.LogDebug("HandleWebSocketClosed");
            _startTask?.SetCanceled();
            _receiveTask?.SetCanceled();
        }

        public static ValueTask<bool> IsSupportedAsync(IJSRuntime jsRuntime)
        {
            if (jsRuntime == null)
                throw new ArgumentNullException(nameof(jsRuntime));

            return jsRuntime.InvokeAsync<bool>(
                "BlazorSignalR.WebSocketsTransport.IsSupported");
        }

        private static class Log
        {
            private static readonly Action<ILogger, TransferFormat, Exception?> StartTransportMessage =
                LoggerMessage.Define<TransferFormat>(LogLevel.Information, new EventId(1, "StartTransport"),
                    "Starting transport. Transfer mode: {TransferFormat}. ");

            private static readonly Action<ILogger, Exception?> TransportStoppedMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(2, "TransportStopped"), "Transport stopped.");

            private static readonly Action<ILogger, Exception?> StartReceiveMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(3, "StartReceive"), "Starting receive loop.");

            private static readonly Action<ILogger, Exception?> ReceiveStoppedMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(4, "ReceiveStopped"), "Receive loop stopped.");

            private static readonly Action<ILogger, Exception?> ReceiveCanceledMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(5, "ReceiveCanceled"), "Receive loop canceled.");

            private static readonly Action<ILogger, Exception?> TransportStoppingMessage =
                LoggerMessage.Define(LogLevel.Information, new EventId(6, "TransportStopping"),
                    "Transport is stopping.");

            private static readonly Action<ILogger, Exception?> SendStartedMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(7, "SendStarted"), "Starting the send loop.");

            private static readonly Action<ILogger, Exception?> SendStoppedMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(8, "SendStopped"), "Send loop stopped.");

            private static readonly Action<ILogger, Exception?> SendCanceledMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(9, "SendCanceled"), "Send loop canceled.");

            private static readonly Action<ILogger, int, Exception?> MessageToAppMessage =
                LoggerMessage.Define<int>(LogLevel.Debug, new EventId(10, "MessageToApp"),
                    "Passing message to application. Payload size: {Count}.");

            private static readonly Action<ILogger, WebSocketCloseStatus?, Exception?> WebSocketClosedMessage =
                LoggerMessage.Define<WebSocketCloseStatus?>(LogLevel.Information, new EventId(11, "WebSocketClosed"),
                    "WebSocket closed by the server. Close status {CloseStatus}.");

            private static readonly Action<ILogger, int, Exception?> MessageReceivedMessage =
                LoggerMessage.Define<int>(LogLevel.Debug,
                    new EventId(12, "MessageReceived"),
                    "Message received.  size: {Count}.");

            private static readonly Action<ILogger, long, Exception?> ReceivedFromAppMessage =
                LoggerMessage.Define<long>(LogLevel.Debug, new EventId(13, "ReceivedFromApp"),
                    "Received message from application. Payload size: {Count}.");

            private static readonly Action<ILogger, Exception?> SendMessageCanceledMessage =
                LoggerMessage.Define(LogLevel.Information, new EventId(14, "SendMessageCanceled"),
                    "Sending a message canceled.");

            private static readonly Action<ILogger, Exception?> ErrorSendingMessageMessage =
                LoggerMessage.Define(LogLevel.Error, new EventId(15, "ErrorSendingMessage"),
                    "Error while sending a message.");

            private static readonly Action<ILogger, Exception?> ClosingWebSocketMessage =
                LoggerMessage.Define(LogLevel.Information, new EventId(16, "ClosingWebSocket"), "Closing WebSocket.");

            private static readonly Action<ILogger, Exception?> ClosingWebSocketFailedMessage =
                LoggerMessage.Define(LogLevel.Information, new EventId(17, "ClosingWebSocketFailed"),
                    "Closing webSocket failed.");

            private static readonly Action<ILogger, Exception?> CancelMessageMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(18, "CancelMessage"),
                    "Canceled passing message to application.");

            private static readonly Action<ILogger, Exception?> StartedTransportMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(19, "StartedTransport"), "Started transport.");

            public static void StartTransport(ILogger? logger, TransferFormat transferFormat)
            {
                if (logger is null)
                    return;

                StartTransportMessage(logger, transferFormat, null);
            }

            public static void TransportStopped(ILogger? logger, Exception? exception)
            {
                if (logger is null)
                    return;

                TransportStoppedMessage(logger, exception);
            }

            public static void StartReceive(ILogger? logger)
            {
                if (logger is null)
                    return;

                StartReceiveMessage(logger, null);
            }

            public static void TransportStopping(ILogger? logger)
            {
                if (logger is null)
                    return;

                TransportStoppingMessage(logger, null);
            }

            public static void MessageToApp(ILogger? logger, int count)
            {
                if (logger is null)
                    return;

                MessageToAppMessage(logger, count, null);
            }

            public static void ReceiveCanceled(ILogger? logger)
            {
                if (logger is null)
                    return;

                ReceiveCanceledMessage(logger, null);
            }

            public static void ReceiveStopped(ILogger? logger)
            {
                if (logger is null)
                    return;

                ReceiveStoppedMessage(logger, null);
            }

            public static void SendStarted(ILogger? logger)
            {
                if (logger is null)
                    return;

                SendStartedMessage(logger, null);
            }

            public static void SendCanceled(ILogger? logger)
            {
                if (logger is null)
                    return;

                SendCanceledMessage(logger, null);
            }

            public static void SendStopped(ILogger? logger)
            {
                if (logger is null)
                    return;

                SendStoppedMessage(logger, null);
            }

            public static void WebSocketClosed(ILogger? logger, WebSocketCloseStatus? closeStatus)
            {
                if (logger is null)
                    return;

                WebSocketClosedMessage(logger, closeStatus, null);
            }

            public static void MessageReceived(ILogger? logger, int count)
            {
                if (logger is null)
                    return;

                MessageReceivedMessage(logger, count, null);
            }

            public static void ReceivedFromApp(ILogger? logger, long count)
            {
                if (logger is null)
                    return;

                ReceivedFromAppMessage(logger, count, null);
            }

            public static void SendMessageCanceled(ILogger? logger)
            {
                if (logger is null)
                    return;

                SendMessageCanceledMessage(logger, null);
            }

            public static void ErrorSendingMessage(ILogger? logger, Exception? exception)
            {
                if (logger is null)
                    return;

                ErrorSendingMessageMessage(logger, exception);
            }

            public static void ClosingWebSocket(ILogger? logger)
            {
                if (logger is null)
                    return;

                ClosingWebSocketMessage(logger, null);
            }

            public static void ClosingWebSocketFailed(ILogger? logger, Exception exception)
            {
                if (logger is null)
                    return;

                ClosingWebSocketFailedMessage(logger, exception);
            }

            public static void CancelMessage(ILogger? logger)
            {
                if (logger is null)
                    return;

                CancelMessageMessage(logger, null);
            }

            public static void StartedTransport(ILogger? logger)
            {
                if (logger is null)
                    return;

                StartedTransportMessage(logger, null);
            }
        }
    }
}
