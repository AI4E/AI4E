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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Blazor.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace AI4E.AspNetCore.Blazor.SignalR
{
    internal class BlazorHttpConnection : ConnectionContext, IConnectionInherentKeepAliveFeature
    {
        private const int MaxRedirects = 100;
        private static readonly Task<string?> NoAccessToken = Task.FromResult<string?>(null);
        private static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(120.0);

        private readonly BlazorHttpConnectionOptions _options;
        private readonly IJSRuntime _jsRuntime;
        private readonly NavigationManager _navigationManager;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger<BlazorHttpConnection>? _logger;
        private readonly HttpClient _httpClient;

        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        private string? _connectionId;
        private IDuplexPipe? _transport;
        private bool _disposed;
        private bool _started;
        private bool _hasInherentKeepAlive;
        private Func<Task<string?>>? _accessTokenProvider;

        public BlazorHttpConnection(
            BlazorHttpConnectionOptions options,
            IJSRuntime jsRuntime,
            NavigationManager navigationManager,
            ILoggerFactory? loggerFactory)
        {
            if (jsRuntime == null)
                throw new ArgumentNullException(nameof(jsRuntime));

            if (navigationManager is null)
                throw new ArgumentNullException(nameof(navigationManager));

            _options = options;
            _jsRuntime = jsRuntime;
            _navigationManager = navigationManager;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory?.CreateLogger<BlazorHttpConnection>();
            _httpClient = CreateHttpClient();
            Features.Set<IConnectionInherentKeepAliveFeature>(this);
        }

        public override string? ConnectionId
        {
            get => _connectionId;
            set => throw new InvalidOperationException(
                "The ConnectionId is set internally and should not be set by user code.");
        }

        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems();

        public override IDuplexPipe Transport
        {
            get
            {
                CheckDisposed();

                if (_transport == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot access the {nameof(Transport)} pipe before the connection has started.");
                }

                return _transport;
            }
            set => throw new NotSupportedException("The transport pipe isn't settable.");
        }

        bool IConnectionInherentKeepAliveFeature.HasInherentKeepAlive => _hasInherentKeepAlive;

        public Task StartAsync()
        {
            return StartAsync(_options.DefaultTransferFormat);
        }

        public async Task StartAsync(TransferFormat transferFormat)
        {
            CheckDisposed();

            if (_started)
            {
                Log.SkippingStart(_logger);
                return;
            }

            await _connectionLock.WaitAsync().ConfigureAwait(false);

            try
            {
                CheckDisposed();
                if (_started)
                {
                    Log.SkippingStart(_logger);
                }
                else
                {
                    Log.Starting(_logger);
                    await SelectAndStartTransport(transferFormat).ConfigureAwait(false);
                    _started = true;
                    Log.Started(_logger);
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task SelectAndStartTransport(TransferFormat transferFormat)
        {
            var uri = _options.Url;

            if (uri is null)
            {
                throw new InvalidOperationException("No uri specified to connect to.");
            }

            // Fix relative url paths
            if (!uri.IsAbsoluteUri || uri.Scheme == Uri.UriSchemeFile && uri.OriginalString.StartsWith("/", StringComparison.Ordinal))
            {
                var baseUrl = new Uri(_navigationManager.BaseUri);
                uri = new Uri(baseUrl, uri);
            }

            _accessTokenProvider = _options.AccessTokenProvider;
            if (_options.SkipNegotiation)
            {
                if (_options.Transports != HttpTransportType.WebSockets)
                    throw new InvalidOperationException(
                        "Negotiation can only be skipped when using the WebSocket transport directly.");
                Log.StartingTransport(_logger, _options.Transports, uri);
                await StartTransport(uri, _options.Transports, transferFormat)
                    .ConfigureAwait(false);
            }
            else
            {
                var redirects = 0;
                NegotiationResponse? negotiationResponse;
                do
                {
                    negotiationResponse = await GetNegotiationResponseAsync(uri).ConfigureAwait(false);
                    if (negotiationResponse.Url != null)
                        uri = new Uri(negotiationResponse.Url);
                    if (negotiationResponse.AccessToken != null)
                    {
                        var accessToken = negotiationResponse.AccessToken;
                        _accessTokenProvider = () => Task.FromResult(accessToken)!;
                    }

                    ++redirects;
                }
                while (negotiationResponse.Url != null && redirects < MaxRedirects);

                if (redirects == MaxRedirects && negotiationResponse.Url != null)
                {
                    throw new InvalidOperationException("Negotiate redirection limit exceeded.");
                }

                var connectUrl = CreateConnectUrl(uri, negotiationResponse.ConnectionId);
                var transferFormatString = transferFormat.ToString();

                foreach (var current in negotiationResponse.AvailableTransports)
                {
                    if (!Enum.TryParse(current.Transport, out HttpTransportType transportType))
                    {
                        Log.TransportNotSupported(_logger, current.Transport);
                    }
                    else
                    {
                        try
                        {
                            if ((transportType & _options.Transports) == HttpTransportType.None)
                            {
                                Log.TransportDisabledByClient(_logger, transportType);
                            }
                            else if (!current.TransferFormats.Contains(transferFormatString, StringComparer.Ordinal))
                            {
                                Log.TransportDoesNotSupportTransferFormat(_logger, transportType, transferFormat);
                            }
                            else
                            {
                                if (negotiationResponse == null)
                                {
                                    connectUrl = CreateConnectUrl(
                                        uri,
                                        (await GetNegotiationResponseAsync(uri).ConfigureAwait(false)).ConnectionId);
                                }

                                Log.StartingTransport(_logger, transportType, connectUrl);
                                await StartTransport(connectUrl, transportType, transferFormat).ConfigureAwait(false);
                                break;
                            }
                        }
#pragma warning disable CA1031
                        catch (Exception exc)
#pragma warning restore CA1031
                        {
                            Log.TransportFailed(_logger, transportType, exc);
                            negotiationResponse = null;
                        }
                    }
                }
            }

            if (_transport == null)
            {
                throw new InvalidOperationException(
                    "Unable to connect to the server with any of the available transports.");
            }

        }

        private async Task StartTransport(Uri connectUrl, HttpTransportType transportType,
            TransferFormat transferFormat)
        {
            var transport = await CreateTransport(transportType).ConfigureAwait(false);
            try
            {
                await transport.StartAsync(connectUrl, transferFormat).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.ErrorStartingTransport(_logger, transportType, ex);
                _transport = null;
                throw;
            }

            _hasInherentKeepAlive = transportType == HttpTransportType.LongPolling;
            _transport = transport;
            Log.TransportStarted(_logger, transportType);
        }

        private async Task<IDuplexPipe> CreateTransport(HttpTransportType availableServerTransports)
        {
            var useWebSockets = (availableServerTransports & HttpTransportType.WebSockets & _options.Transports) ==
                                 HttpTransportType.WebSockets;

            if (useWebSockets && (_options.Implementations & BlazorTransportType.ManagedWebSockets) ==
                BlazorTransportType.ManagedWebSockets)
            {
                // TODO: Add C# websocket implementation
                //                    return (ITransport) new WebSocketsTransport(this._httpConnectionOptions, this._loggerFactory,
                //                            this._accessTokenProvider);
                //                throw new NotImplementedException("Websocket support has not been implemented!");
            }

            if (useWebSockets && (_options.Implementations & BlazorTransportType.JsWebSockets) ==
                BlazorTransportType.JsWebSockets && await BlazorWebSocketsTransport.IsSupportedAsync(_jsRuntime))
            {
                return new BlazorWebSocketsTransport(
                    await GetAccessTokenAsync().ConfigureAwait(false), _jsRuntime, _loggerFactory);
            }

            var useSSE = (availableServerTransports & HttpTransportType.ServerSentEvents & _options.Transports) ==
                          HttpTransportType.ServerSentEvents;

            if (useSSE && (_options.Implementations & BlazorTransportType.JsServerSentEvents) ==
                BlazorTransportType.JsServerSentEvents && await BlazorServerSentEventsTransport.IsSupportedAsync(_jsRuntime))
            {
                return new BlazorServerSentEventsTransport(
                    await GetAccessTokenAsync().ConfigureAwait(false),
                    _httpClient,
                    _jsRuntime,
                    _loggerFactory);
            }

            if (useSSE && (_options.Implementations & BlazorTransportType.ManagedServerSentEvents) ==
                BlazorTransportType.ManagedServerSentEvents && false)
            {
                var duplexPipe = ReflectionHelper.CreateInstance(
                    typeof(HttpConnection).Assembly,
                    "Microsoft.AspNetCore.Http.Connections.Client.Internal.ServerSentEventsTransport",
                    _httpClient,
                    _loggerFactory ?? NullLoggerFactory.Instance) as IDuplexPipe;
                Debug.Assert(duplexPipe != null);
                return duplexPipe!;
            }

            var useLongPolling = (availableServerTransports & HttpTransportType.LongPolling & _options.Transports) ==
                                  HttpTransportType.LongPolling;

            if (useLongPolling && (_options.Implementations & BlazorTransportType.JsLongPolling) ==
                BlazorTransportType.JsLongPolling)
            {
                // TODO: Add JS long polling implementation
            }

            if (useLongPolling && (_options.Implementations & BlazorTransportType.ManagedLongPolling) ==
                BlazorTransportType.ManagedLongPolling)
            {
                var duplexPipe = ReflectionHelper.CreateInstance(
                    typeof(HttpConnection).Assembly,
                    "Microsoft.AspNetCore.Http.Connections.Client.Internal.LongPollingTransport",
                    _httpClient,
                    _loggerFactory ?? NullLoggerFactory.Instance) as IDuplexPipe;
                Debug.Assert(duplexPipe != null);
                return duplexPipe!;
            }

            throw new InvalidOperationException(
                "No requested transports available on the server (and are enabled locally).");
        }

        private async Task<NegotiationResponse> GetNegotiationResponseAsync(Uri uri)
        {
            var negotiationResponse = await NegotiateAsync(uri, _httpClient, _logger)
                .ConfigureAwait(false);
            _connectionId = negotiationResponse.ConnectionId;
            return negotiationResponse;
        }

        private async Task<NegotiationResponse> NegotiateAsync(Uri url, HttpClient httpClient, ILogger? logger)
        {
            NegotiationResponse negotiationResponse;
            try
            {
                Log.EstablishingConnection(logger, url);
                var uriBuilder = new UriBuilder(url);

                if (!uriBuilder.Path.EndsWith("/", StringComparison.Ordinal))
                {
                    uriBuilder.Path += "/";
                }

                uriBuilder.Path += "negotiate";
                using var request = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri)
                {
                    Version = new Version(1, 1)
                };

                using var response1 = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                response1.EnsureSuccessStatusCode();
                NegotiationResponse response2;
                var content = await response1.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                response2 = NegotiateProtocol.ParseResponse(content);
                Log.ConnectionEstablished(_logger, response2.ConnectionId);
                negotiationResponse = response2;
            }
            catch (Exception ex)
            {
                Log.ErrorWithNegotiation(logger, url, ex);
                throw;
            }

            return negotiationResponse;
        }

        private static Uri CreateConnectUrl(Uri url, string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                throw new FormatException("Invalid connection id.");

            return AppendQueryString(url, "id=" + connectionId);
        }

        private static Uri AppendQueryString(Uri url, string qs)
        {
            if (string.IsNullOrEmpty(qs))
                return url;
            var uriBuilder = new UriBuilder(url);
            var query = uriBuilder.Query;
            if (!string.IsNullOrEmpty(uriBuilder.Query))
                query += "&";
            var str = query + qs;
            if (str.Length > 0 && str[0] == '?')
                str = str.Substring(1);
            uriBuilder.Query = str;
            return uriBuilder.Uri;
        }

        private HttpClient CreateHttpClient()
        {
#pragma warning disable IDE0068
            HttpMessageHandler handler = new WebAssemblyHttpMessageHandler();
#pragma warning restore IDE0068

            if (_options.HttpMessageHandlerFactory != null)
            {
                handler = _options.HttpMessageHandlerFactory(handler);

                if (handler == null)
                    throw new InvalidOperationException("Configured HttpMessageHandlerFactory did not return a value.");
            }

            handler = new BlazorAccessTokenHttpMessageHandler(handler, this);

            var httpClient = new HttpClient(new LoggingHttpMessageHandler(handler, _loggerFactory))
            {
                BaseAddress = new Uri(_navigationManager.BaseUri),
                Timeout = HttpClientTimeout
            };
            //            httpClient.DefaultRequestHeaders.UserAgent.Add(Constants.UserAgentHeader);
            if (_options.Headers != null)
            {
                foreach (var header in _options.Headers)
                {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            httpClient.DefaultRequestHeaders.Remove("X-Requested-With");
            httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
            return httpClient;
        }

        internal Task<string?> GetAccessTokenAsync()
        {
            return _accessTokenProvider == null ? NoAccessToken : _accessTokenProvider();
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            await _connectionLock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!_disposed && _started)
                {
                    Log.DisposingHttpConnection(_logger);
                    try
                    {
                        if (_transport != null)
                        {
                            await _transport.StopAsync().ConfigureAwait(false);
                        }
                    }
#pragma warning disable CA1031
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        Log.TransportThrewExceptionOnStop(_logger, ex);
                    }

                    Log.Disposed(_logger);
                }
                else
                    Log.SkippingDispose(_logger);
            }
            finally
            {
                _disposed = true;
                _connectionLock.Release();
            }
        }

        private void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BlazorHttpConnection));
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception?> StartingMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(1, "Starting"), "Starting HttpConnection.");

            private static readonly Action<ILogger, Exception?> SkippingStartMessage = LoggerMessage.Define(LogLevel.Debug,
                new EventId(2, "SkippingStart"), "Skipping start, connection is already started.");

            private static readonly Action<ILogger, Exception?> StartedMessage = LoggerMessage.Define(LogLevel.Information,
                new EventId(3, "Started"), "HttpConnection Started.");

            private static readonly Action<ILogger, Exception?> DisposingHttpConnectionMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(4, "DisposingHttpConnection"),
                    "Disposing HttpConnection.");

            private static readonly Action<ILogger, Exception?> SkippingDisposeMessage = LoggerMessage.Define(LogLevel.Debug,
                new EventId(5, "SkippingDispose"), "Skipping dispose, connection is already disposed.");

            private static readonly Action<ILogger, Exception?> DisposedMessage = LoggerMessage.Define(LogLevel.Information,
                new EventId(6, "Disposed"), "HttpConnection Disposed.");

            private static readonly Action<ILogger, string, Uri, Exception?> StartingTransportMessage =
                LoggerMessage.Define<string, Uri>(LogLevel.Debug, new EventId(7, "StartingTransport"),
                    "Starting transport '{Transport}' with Url: {Url}.");

            private static readonly Action<ILogger, Uri, Exception?> EstablishingConnectionMessage =
                LoggerMessage.Define<Uri>(LogLevel.Debug, new EventId(8, "EstablishingConnection"),
                    "Establishing connection with server at '{Url}'.");

            private static readonly Action<ILogger, string, Exception?> ConnectionEstablishedMessage =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(9, "Established"),
                    "Established connection '{ConnectionId}' with the server.");

            private static readonly Action<ILogger, Uri, Exception?> ErrorWithNegotiationMessage =
                LoggerMessage.Define<Uri>(LogLevel.Error, new EventId(10, "ErrorWithNegotiation"),
                    "Failed to start connection. Error getting negotiation response from '{Url}'.");

            private static readonly Action<ILogger, HttpTransportType, Exception?> ErrorStartingTransportMessage =
                LoggerMessage.Define<HttpTransportType>(LogLevel.Error, new EventId(11, "ErrorStartingTransport"),
                    "Failed to start connection. Error starting transport '{Transport}'.");

            private static readonly Action<ILogger, string, Exception?> TransportNotSupportedMessage =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(12, "TransportNotSupported"),
                    "Skipping transport {TransportName} because it is not supported by this client.");

            private static readonly Action<ILogger, string, string, Exception?> TransportDoesNotSupportTransferFormatMessage =
                LoggerMessage.Define<string, string>(LogLevel.Debug,
                    new EventId(13, "TransportDoesNotSupportTransferFormat"),
                    "Skipping transport {TransportName} because it does not support the requested transfer format '{TransferFormat}'.");

            private static readonly Action<ILogger, string, Exception?> TransportDisabledByClientMessage =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(14, "TransportDisabledByClient"),
                    "Skipping transport {TransportName} because it was disabled by the client.");

            private static readonly Action<ILogger, string, Exception?> TransportFailedMessage =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(15, "TransportFailed"),
                    "Skipping transport {TransportName} because it failed to initialize.");

            private static readonly Action<ILogger, Exception?> WebSocketsNotSupportedByOperatingSystemMessage =
                LoggerMessage.Define(LogLevel.Debug, new EventId(16, "WebSocketsNotSupportedByOperatingSystem"),
                    "Skipping WebSockets because they are not supported by the operating system.");

            private static readonly Action<ILogger, Exception?> TransportThrewExceptionOnStopMessage =
                LoggerMessage.Define(LogLevel.Error, new EventId(17, "TransportThrewExceptionOnStop"),
                    "The transport threw an exception while stopping.");

            private static readonly Action<ILogger, HttpTransportType, Exception?> TransportStartedMessage =
                LoggerMessage.Define<HttpTransportType>(LogLevel.Debug, new EventId(18, "TransportStarted"),
                    "Transport '{Transport}' started.");

            public static void Starting(ILogger? logger)
            {
                if (logger is null)
                    return;

                StartingMessage(logger, null);
            }

            public static void SkippingStart(ILogger? logger)
            {
                if (logger is null)
                    return;

                SkippingStartMessage(logger, null);
            }

            public static void Started(ILogger? logger)
            {
                if (logger is null)
                    return;

                StartedMessage(logger, null);
            }

            public static void DisposingHttpConnection(ILogger? logger)
            {
                if (logger is null)
                    return;

                DisposingHttpConnectionMessage(logger, null);
            }

            public static void SkippingDispose(ILogger? logger)
            {
                if (logger is null)
                    return;

                SkippingDisposeMessage(logger, null);
            }

            public static void Disposed(ILogger? logger)
            {
                if (logger is null)
                    return;

                DisposedMessage(logger, null);
            }

            public static void StartingTransport(ILogger? logger, HttpTransportType transportType, Uri url)
            {
                if (logger is null)
                    return;

                if (!logger.IsEnabled(LogLevel.Debug))
                    return;

                StartingTransportMessage(logger, transportType.ToString(), url, null);
            }

            public static void EstablishingConnection(ILogger? logger, Uri url)
            {
                if (logger is null)
                    return;

                EstablishingConnectionMessage(logger, url, null);
            }

            public static void ConnectionEstablished(ILogger? logger, string connectionId)
            {
                if (logger is null)
                    return;

                ConnectionEstablishedMessage(logger, connectionId, null);
            }

            public static void ErrorWithNegotiation(ILogger? logger, Uri url, Exception exception)
            {
                if (logger is null)
                    return;

                ErrorWithNegotiationMessage(logger, url, exception);
            }

            public static void ErrorStartingTransport(ILogger? logger, HttpTransportType transportType,
                Exception exception)
            {
                if (logger is null)
                    return;

                ErrorStartingTransportMessage(logger, transportType, exception);
            }

            public static void TransportNotSupported(ILogger? logger, string transport)
            {
                if (logger is null)
                    return;

                TransportNotSupportedMessage(logger, transport, null);
            }

            public static void TransportDoesNotSupportTransferFormat(ILogger? logger, HttpTransportType transport,
                TransferFormat transferFormat)
            {
                if (logger is null)
                    return;

                if (!logger.IsEnabled(LogLevel.Debug))
                    return;

                TransportDoesNotSupportTransferFormatMessage(logger, transport.ToString(), transferFormat.ToString(),
                    null);
            }

            public static void TransportDisabledByClient(ILogger? logger, HttpTransportType transport)
            {
                if (logger is null)
                    return;

                if (!logger.IsEnabled(LogLevel.Debug))
                    return;
                TransportDisabledByClientMessage(logger, transport.ToString(), null);
            }

            public static void TransportFailed(ILogger? logger, HttpTransportType transport, Exception ex)
            {
                if (logger is null)
                    return;

                if (!logger.IsEnabled(LogLevel.Debug))
                    return;

                TransportFailedMessage(logger, transport.ToString(), ex);
            }

            public static void WebSocketsNotSupportedByOperatingSystem(ILogger? logger)
            {
                if (logger is null)
                    return;

                WebSocketsNotSupportedByOperatingSystemMessage(logger, null);
            }

            public static void TransportThrewExceptionOnStop(ILogger? logger, Exception ex)
            {
                if (logger is null)
                    return;

                TransportThrewExceptionOnStopMessage(logger, ex);
            }

            public static void TransportStarted(ILogger? logger, HttpTransportType transportType)
            {
                if (logger is null)
                    return;

                TransportStartedMessage(logger, transportType, null);
            }
        }
    }
}
