using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.RPC;

namespace AI4E.Modularity.Debugging
{
    public sealed class DebugRouteStore : IRouteStore
    {
        private IProxy<RouteStoreSkeleton> _proxy;
        private readonly RPCHost _rpcHost;
        private readonly Task _initialization;

        public DebugRouteStore(RPCHost rpcHost)
        {
            if (rpcHost == null)
                throw new ArgumentNullException(nameof(rpcHost));

            _rpcHost = rpcHost;
            _initialization = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _proxy = await _rpcHost.ActivateAsync<RouteStoreSkeleton>(ActivationMode.Create, cancellation: default);
        }

        public async Task<bool> AddRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.AddRouteAsync(localEndPoint, messageType, CancellationToken.None));
        }

        public async Task<bool> RemoveRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.RemoveRouteAsync(localEndPoint, messageType, CancellationToken.None));
        }

        public async Task RemoveRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.RemoveRouteAsync(localEndPoint, CancellationToken.None));
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.GetRoutesAsync(messageType, CancellationToken.None));
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.GetRoutesAsync(CancellationToken.None));
        }
    }
}
