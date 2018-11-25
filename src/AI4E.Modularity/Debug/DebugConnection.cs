using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Proxying;
using Microsoft.Extensions.Logging;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugConnection : IDisposable
    {
        private readonly IPEndPoint _debugPort;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DebugConnection> _logger;

        private readonly DisposableAsyncLazy<TcpClient> _tcpClientLazy;
        private readonly DisposableAsyncLazy<ProxyHost> _proxyHostLazy;

        public DebugConnection(IPEndPoint debugPort, IDateTimeProvider dateTimeProvider, IServiceProvider serviceProvider, ILoggerFactory loggerFactory = null)
        {
            if (debugPort == null)
                throw new ArgumentNullException(nameof(debugPort));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _debugPort = debugPort;
            _dateTimeProvider = dateTimeProvider;
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;

            _logger = _loggerFactory?.CreateLogger<DebugConnection>();

            _tcpClientLazy = new DisposableAsyncLazy<TcpClient>(
                factory: CreateTcpClientAsync,
                disposal: tcpClient => { tcpClient.Dispose(); return Task.CompletedTask; },
                options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);

            _proxyHostLazy = new DisposableAsyncLazy<ProxyHost>(
                factory: CreateProxyHostAsync,
                disposal: proxyHost => proxyHost.DisposeAsync(),
                options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        private async Task<TcpClient> CreateTcpClientAsync(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Trying to connect to debug port {_debugPort}.");
            var tcpClient = new TcpClient(_debugPort.AddressFamily);
            try
            {
                var delay = TimeSpan.FromSeconds(1);
                var maxDelay = TimeSpan.FromSeconds(6);
                var delayFactor = 1.1;

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        await tcpClient.ConnectAsync(_debugPort.Address, _debugPort.Port).WithCancellation(cancellation);
                        break;
                    }
                    catch (SocketException exc) when (exc.SocketErrorCode == SocketError.ConnectionRefused) { }

                    _logger?.LogWarning($"Debug port unreachable. Trying again in {delay.TotalSeconds.ToString("0.00")}sec.");

                    await Task.Delay(delay, cancellation);

                    delay = new TimeSpan((long)(delay.Ticks * delayFactor));

                    if (delay > maxDelay)
                    {
                        delay = maxDelay;
                    }
                }
            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }

            return tcpClient;
        }

        private async Task<ProxyHost> CreateProxyHostAsync(CancellationToken cancellation)
        {
            var tcpClient = await _tcpClientLazy.Task.WithCancellation(cancellation);

            var logger = _loggerFactory?.CreateLogger<DisposeAwareStream>();

            // TODO: Graceful shutdown
            var stream = new DisposeAwareStream(tcpClient.GetStream(), _dateTimeProvider, () => { Environment.FailFast(""); return Task.CompletedTask; }, logger);
            var proxyHost = new ProxyHost(stream, _serviceProvider);

            return proxyHost;
        }

        public async ValueTask<IPEndPoint> GetLocalAddressAync(CancellationToken cancellation)
        {
            var tcpClient = await _tcpClientLazy.Task.WithCancellation(cancellation);
            return tcpClient.Client.LocalEndPoint as IPEndPoint;
        }

        public ValueTask<ProxyHost> GetProxyHostAsync(CancellationToken cancellation)
        {
            return new ValueTask<ProxyHost>(_proxyHostLazy.Task.WithCancellation(cancellation));
        }

        public void Dispose()
        {
            try
            {
                _proxyHostLazy.Dispose();
            }
            finally
            {
                _tcpClientLazy.Dispose();
            }
        }
    }
}
