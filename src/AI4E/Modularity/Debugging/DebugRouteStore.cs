using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.RPC;

namespace AI4E.Modularity.Debugging
{
    public sealed class DebugRouteStore : IRouteStore
    {
        private readonly IProxy<IRouteStore> _proxy;

        public DebugRouteStore(IProxy<IRouteStore> proxy)
        {
            if (proxy == null)
                throw new ArgumentNullException(nameof(proxy));

            _proxy = proxy;
        }

        public Task<bool> AddRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            return _proxy.ExecuteAsync(p => p.AddRouteAsync(localEndPoint, messageType, CancellationToken.None));
        }

        public Task<bool> RemoveRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            return _proxy.ExecuteAsync(p => p.RemoveRouteAsync(localEndPoint, messageType, CancellationToken.None));
        }

        public Task RemoveRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            return _proxy.ExecuteAsync(p => p.RemoveRouteAsync(localEndPoint, CancellationToken.None));
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            return await _proxy.ExecuteAsync(p => p.GetRoutesAsync(messageType, CancellationToken.None));
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(CancellationToken cancellation)
        {
            return await _proxy.ExecuteAsync(p => p.GetRoutesAsync(CancellationToken.None));
        }
    }
}
