using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Modularity.Metadata;
using AI4E.Routing;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Proxying;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugConnection : IDisposable
    {
        private readonly IMetadataAccessor _metadataAccessor;
        private readonly ModularityDebugOptions _options;
        private readonly RemoteMessagingOptions _remoteOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DebugConnection> _logger;

        private readonly Lazy<IPEndPoint> _debugPort; // TODO: Rename

        private readonly DisposableAsyncLazy<TcpClient> _tcpClientLazy;
        private readonly DisposableAsyncLazy<ProxyHost> _proxyHostLazy;

        public DebugConnection(IMetadataAccessor metadataAccessor,
                               IOptions<ModularityDebugOptions> optionsAccessor,
                               IOptions<RemoteMessagingOptions> remoteOptionsAccessor,
                               IServiceProvider serviceProvider,
                               ILoggerFactory loggerFactory = null)
        {
       
            if (metadataAccessor == null)
                throw new ArgumentNullException(nameof(metadataAccessor));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            if (remoteOptionsAccessor == null)
                throw new ArgumentNullException(nameof(remoteOptionsAccessor));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _metadataAccessor = metadataAccessor;
            _options = optionsAccessor.Value ?? new ModularityDebugOptions();
            _remoteOptions = remoteOptionsAccessor.Value ?? new RemoteMessagingOptions();
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;

            _logger = _loggerFactory?.CreateLogger<DebugConnection>();
            _debugPort = new Lazy<IPEndPoint>(() => IPEndPointConverter.AddressFromString(_options.DebugConnection), LazyThreadSafetyMode.PublicationOnly);

            _tcpClientLazy = new DisposableAsyncLazy<TcpClient>(
                factory: CreateTcpClientAsync,
                disposal: tcpClient => { tcpClient.Dispose(); return Task.CompletedTask; },
                options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);

            _proxyHostLazy = new DisposableAsyncLazy<ProxyHost>(
                factory: CreateProxyHostAsync,
                disposal: proxyHost => proxyHost.DisposeAsync().AsTask(), // TODO: This should accept a ValueTask
                options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }


        public IPEndPoint DebugPort => _debugPort.Value; // TODO: Rename

        private async Task<TcpClient> CreateTcpClientAsync(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Trying to connect to debug port {DebugPort}.");
            var tcpClient = new TcpClient(DebugPort.AddressFamily);
            try
            {
                var delay = TimeSpan.FromSeconds(1);
                var maxDelay = TimeSpan.FromSeconds(6);
                var delayFactor = 1.1;

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        await tcpClient.ConnectAsync(DebugPort.Address, DebugPort.Port).WithCancellation(cancellation);
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
            var stream = new DisposeAwareStream(tcpClient.GetStream(), () => { Environment.FailFast(""); return Task.CompletedTask; }, logger);

            var endPoint = _remoteOptions.LocalEndPoint;
            var metadata = await _metadataAccessor.GetMetadataAsync(cancellation);
            var module = metadata.Module;
            var version = metadata.Version;

            var properties = new DebugModuleProperties(endPoint, module, version);
            await properties.WriteAsync(stream, cancellation);
            return new ProxyHost(stream, _serviceProvider);
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
