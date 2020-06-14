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
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace AI4E.AspNetCore.Blazor.SignalR
{
    public class BlazorServerSentEventsTransport : IDuplexPipe
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger? _logger;
        private volatile Exception? _error;
        private readonly CancellationTokenSource _transportCts = new CancellationTokenSource();

        private IDuplexPipe? _transport;
        private IDuplexPipe? _application;

#pragma warning disable CA1822
        public string InternalSSEId { [JSInvokable] get; }
        public string? SSEAccessToken { [JSInvokable] get; }
#pragma warning restore CA1822

        internal Task Running { get; private set; } = Task.CompletedTask;

        public PipeReader? Input => _transport?.Input;
        public PipeWriter? Output => _transport?.Output;

        private TaskCompletionSource<object>? _jsTask;

        public BlazorServerSentEventsTransport(string? token, HttpClient httpClient, IJSRuntime jsRuntime, ILoggerFactory? loggerFactory)
        {
            if (jsRuntime == null)
                throw new ArgumentNullException(nameof(jsRuntime));

            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
            _logger = loggerFactory?.CreateLogger<BlazorServerSentEventsTransport>();
            InternalSSEId = Guid.NewGuid().ToString();
            SSEAccessToken = token;
        }

        public Task StartAsync(Uri url, TransferFormat transferFormat)
        {
            if (url is null)
                throw new ArgumentNullException(nameof(url));

            if (transferFormat != TransferFormat.Text)
            {
                throw new ArgumentException(
                    $"The '{transferFormat}' transfer format is not supported by this transport.",
                    nameof(transferFormat));
            }

            Log.StartTransport(_logger, transferFormat);

            // Create pipe
            var options = ClientPipeOptions.DefaultOptions;
            var pair = DuplexPipe.CreateConnectionPair(options, options);

            _transport = pair.Transport;
            _application = pair.Application;

            // Start streams
            Running = ProcessAsync(url);

            return Task.CompletedTask;
        }

        private async Task ProcessAsync(Uri url)
        {
            // Start sending and receiving
            Debug.Assert(_application != null);
            var receiving = ProcessEventStream(url.ToString(), _transportCts.Token);
            var sending = SendUtils.SendMessages(url, _application!, _httpClient, _logger, cancellationToken: default);

            // Wait for send or receive to complete
            var trigger = await Task.WhenAny(receiving, sending).ConfigureAwait(false);

            if (trigger == receiving)
            {
                // Cancel the application so that ReadAsync yields
                _application!.Input.CancelPendingRead();

                await sending.ConfigureAwait(false);
            }
            else
            {
                // Set the sending error so we communicate that to the application
                _error = sending.IsFaulted ? (sending.Exception?.InnerException ?? sending.Exception) : null;

                _transportCts.Cancel();

                // Cancel any pending flush so that we can quit
                _application!.Output.CancelPendingFlush();

                await receiving.ConfigureAwait(false);
            }
        }

        private async Task ProcessEventStream(string url, CancellationToken transportCtsToken)
        {
            Log.StartReceive(_logger);

            try
            {
                // Creates a task to represent the SSE js processing
                var task = new TaskCompletionSource<object>();
                _jsTask = task;

                // Create connection
                await _jsRuntime.InvokeAsync<object>(
                    "BlazorSignalR.ServerSentEventsTransport.CreateConnection", url, DotNetObjectReference.Create(this));

                // If canceled, stop fake processing
                transportCtsToken.Register(() => { task.SetCanceled(); });

                // Wait until js side stops
                await task.Task.ConfigureAwait(false);

                if (task.Task.IsCanceled)
                {
                    Log.ReceiveCanceled(_logger);
                }
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger?.LogDebug($"SSE JS Side error {ex.Message}");
                _error = ex;
            }
            finally
            {
                Debug.Assert(_application != null);
                _application!.Output.Complete(_error);

                Log.ReceiveStopped(_logger);

                // Close JS side SSE
                await CloseSSEAsync().ConfigureAwait(false);
            }
        }

        [JSInvokable]
        public void HandleSSEMessage(string msg)
        {
            if (msg is null)
                throw new ArgumentNullException(nameof(msg));

            _logger?.LogDebug($"HandleSSEMessage \"{msg}\"");

            // Decode data
            Log.ParsingSSE(_logger, msg.Length);

            var data = Convert.FromBase64String(msg);
            Log.MessageToApplication(_logger, data.Length);

            // Write to stream
            Debug.Assert(_application != null);
            var flushResult = _application!.Output.WriteAsync(data).Result;

            // Handle cancel
            if (flushResult.IsCanceled || flushResult.IsCompleted)
            {
                Log.EventStreamEnded(_logger);

                Debug.Assert(_jsTask != null);
                _jsTask!.SetCanceled();
            }
        }

        [JSInvokable]
        public void HandleSSEError(string msg)
        {
            _logger?.LogDebug($"HandleSSEError \"{msg}\"");
            Debug.Assert(_jsTask != null);
            _jsTask!.SetException(new Exception(msg));
        }

        [JSInvokable]
        public void HandleSSEOpened()
        {
            _logger?.LogDebug("HandleSSEOpened");
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
            Debug.Assert(_jsTask != null);
            _jsTask!.SetCanceled();
            await CloseSSEAsync().ConfigureAwait(false);

            // Cleanup managed side
            Debug.Assert(_transport != null);
            _transport!.Output.Complete();
            _transport!.Input.Complete();

            _application.Input.CancelPendingRead();

            try
            {
                await Running.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.TransportStopped(_logger, ex);
                throw;
            }

            Log.TransportStopped(_logger, null);
        }

        public async Task CloseSSEAsync()
        {
            try
            {
                await _jsRuntime.InvokeAsync<object>(
                    "BlazorSignalR.ServerSentEventsTransport.CloseConnection", DotNetObjectReference.Create(this));
            }
#pragma warning disable CA1031
            catch (Exception e)
#pragma warning restore CA1031
            {
                _logger?.LogError($"Failed to stop SSE {e}");
            }
        }

        public static ValueTask<bool> IsSupportedAsync(IJSRuntime jsRuntime)
        {
            if (jsRuntime == null)
                throw new ArgumentNullException(nameof(jsRuntime));

            return jsRuntime.InvokeAsync<bool>(
                "BlazorSignalR.ServerSentEventsTransport.IsSupported");
        }

        private static class Log
        {
            private static readonly Action<ILogger, TransferFormat, Exception?> StartTransportMessage =
                LoggerMessage.Define<TransferFormat>(LogLevel.Information, new EventId(1, "StartTransport"),
                    "Starting transport. Transfer mode: {TransferFormat}.");

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

            private static readonly Action<ILogger, int, Exception?> MessageToApplicationMessage =
                LoggerMessage.Define<int>(LogLevel.Debug, new EventId(7, "MessageToApplication"),
                    "Passing message to application. Payload size: {Count}.");

            private static readonly Action<ILogger, Exception?> EventStreamEndedMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(8, "EventStreamEnded"),
                    "Server-Sent Event Stream ended.");

            private static readonly Action<ILogger, long, Exception?> ParsingSSEMessage =
                LoggerMessage.Define<long>(LogLevel.Debug, new EventId(9, "ParsingSSE"),
                    "Received {Count} bytes. Parsing SSE frame.");

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

            public static void MessageToApplication(ILogger? logger, int count)
            {
                if (logger is null)
                    return;

                MessageToApplicationMessage(logger, count, null);
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

            public static void EventStreamEnded(ILogger? logger)
            {
                if (logger is null)
                    return;

                EventStreamEndedMessage(logger, null);
            }

            public static void ParsingSSE(ILogger? logger, long bytes)
            {
                if (logger is null)
                    return;

                ParsingSSEMessage(logger, bytes, null);
            }
        }
    }
}
