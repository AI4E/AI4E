using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils.Async;
using AI4E.Utils.Proxying;
using Microsoft.Extensions.Logging;
using static AI4E.Modularity.Debug.DebugRouteEndPoint;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugRoutingSystem : IRoutingSystem
    {
        private readonly DebugConnection _debugConnection;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<Debug.DebugRoutingSystem> _logger;
        private readonly DisposableAsyncLazy<IProxy<RoutingSystemSkeleton>> _proxyLazy;

        public DebugRoutingSystem(DebugConnection debugConnection, ILoggerFactory loggerFactory = null)
        {
            if (debugConnection is null)
                throw new ArgumentNullException(nameof(debugConnection));

            _debugConnection = debugConnection;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<DebugRoutingSystem>();

            _proxyLazy = new DisposableAsyncLazy<IProxy<RoutingSystemSkeleton>>(
               factory: CreateProxyAsync,
               disposal: p => p.DisposeAsync().AsTask(), // TODO: This should accept a ValueTask
               options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        private async Task<IProxy<RoutingSystemSkeleton>> CreateProxyAsync(CancellationToken cancellation)
        {
            var proxyHost = await _debugConnection.GetProxyHostAsync(cancellation);

            try
            {
                return await proxyHost.CreateAsync<RoutingSystemSkeleton>(cancellation);
            }
            catch (OperationCanceledException)
            {
                proxyHost?.Dispose();
                throw;
            }
        }

        private Task<IProxy<RoutingSystemSkeleton>> GetProxyAsync(CancellationToken cancellation)
        {
            return _proxyLazy.Task.WithCancellation(cancellation);
        }

        public void Dispose()
        {
            _proxyLazy.Dispose();
        }

        // TODO: Add an address to end-point lookup and short-circuit if possible.

        public async ValueTask<IRouteEndPoint> CreateEndPointAsync(RouteEndPointAddress endPoint, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);
            var endPointProxy = await proxy.ExecuteAsync(p => p.CreateEndPointAsync(endPoint, cancellation));
            var logger = _loggerFactory?.CreateLogger<DebugRouteEndPoint>();
            return new DebugRouteEndPoint(endPointProxy, logger);
        }

        public async ValueTask<IRouteEndPoint> GetEndPointAsync(RouteEndPointAddress endPoint, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);
            var endPointProxy = await proxy.ExecuteAsync(p => p.GetEndPointAsync(endPoint, cancellation));
            var logger = _loggerFactory?.CreateLogger<DebugRouteEndPoint>();
            return new DebugRouteEndPoint(endPointProxy, logger);
        }

        private sealed class RoutingSystemSkeleton
        {
            private readonly IRoutingSystem _routingSystem;

            public RoutingSystemSkeleton(IRoutingSystem routingSystem)
            {
                if (routingSystem is null)
                    throw new ArgumentNullException(nameof(routingSystem));

                _routingSystem = routingSystem;
            }

            public async Task<IProxy<RouteEndPointSkeleton>> GetEndPointAsync(
                RouteEndPointAddress endPoint,
                CancellationToken cancellation)
            {
                // TODO: Check whether the module is allowed to access the end-point?

                var routeEndPoint = await _routingSystem.GetEndPointAsync(endPoint, cancellation);
                var skeleton = new RouteEndPointSkeleton(routeEndPoint);
                return ProxyHost.CreateProxy(skeleton, ownsInstance: false);
            }

            public async Task<IProxy<RouteEndPointSkeleton>> CreateEndPointAsync(
                RouteEndPointAddress endPoint,
                CancellationToken cancellation)
            {
                // TODO: Check whether the module is allowed to access the end-point?

                var routeEndPoint = await _routingSystem.CreateEndPointAsync(endPoint, cancellation);
                var skeleton = new RouteEndPointSkeleton(routeEndPoint);
                return ProxyHost.CreateProxy(skeleton, ownsInstance: true);
            }
        }
    }
}
