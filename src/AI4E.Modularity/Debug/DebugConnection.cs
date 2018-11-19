using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Proxying;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugConnection : IAsyncDisposable
    {
        private readonly IPEndPoint _debugPort;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DebugConnection> _logger;
        private readonly AsyncInitializationHelper<(IPEndPoint localAddress, ProxyHost proxyHost)> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

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
            _initializationHelper = new AsyncInitializationHelper<(IPEndPoint localAddress, ProxyHost proxyHost)>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        public async ValueTask<IPEndPoint> GetLocalAddressAync(CancellationToken cancellation)
        {
            var (localAddress, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);
            return localAddress;
        }

        public async ValueTask<ProxyHost> GetProxyHostAsync(CancellationToken cancellation)
        {
            var (_, proxyHost) = await _initializationHelper.Initialization.WithCancellation(cancellation);
            return proxyHost;
        }

        private async Task<(IPEndPoint localAddress, ProxyHost proxyHost)> InitializeInternalAsync(CancellationToken cancellation)
        {
            // TODO: Is the tcp client closed on disposal?
            var tcpClient = new TcpClient(_debugPort.AddressFamily);

            _logger?.LogDebug($"Trying to connect to debug port {_debugPort}.");

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

            _logger?.LogInformation($"Successfully established connection to debug port.");

            var localAddress = tcpClient.Client.LocalEndPoint as IPEndPoint;
            var logger = _loggerFactory?.CreateLogger<DisposeAwareStream>();

            // TODO: Graceful shutdown
            var stream = new DisposeAwareStream(tcpClient.GetStream(), _dateTimeProvider, () => { Environment.FailFast(""); return Task.CompletedTask; }, logger);
            var proxyHost = new ProxyHost(stream, _serviceProvider);

            return (localAddress, proxyHost);
        }

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            var (success, (_, proxyHost)) = await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);

            if (success)
            {
                Assert(proxyHost != null);

                await proxyHost.DisposeAsync();
            }
        }

        #endregion
    }
}
