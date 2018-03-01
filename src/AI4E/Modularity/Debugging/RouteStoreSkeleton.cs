using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace AI4E.Modularity.Debugging
{
    public sealed class RouteStoreSkeleton : IDisposable
    {
        private readonly IRouteStore _routeStore;
        private readonly HashSet<EndPointRoute> _activeRoutes = new HashSet<EndPointRoute>();
        private readonly AsyncLock _lock = new AsyncLock();
        private volatile object _isDisposed;

        public RouteStoreSkeleton(IRouteStore routeStore)
        {
            if (routeStore == null)
                throw new ArgumentNullException(nameof(routeStore));

            _routeStore = routeStore;
        }

        public async Task<bool> AddRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            var result = await _routeStore.AddRouteAsync(localEndPoint, messageType, cancellation);

            using (await _lock.LockAsync())
            {
                _activeRoutes.Add(localEndPoint);
            }

            return result;
        }

        public Task<bool> RemoveRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            return _routeStore.RemoveRouteAsync(localEndPoint, messageType, cancellation);
        }

        public async Task RemoveRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            await _routeStore.RemoveRouteAsync(localEndPoint, cancellation);

            using (await _lock.LockAsync())
            {
                _activeRoutes.Remove(localEndPoint);
            }
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            return (await _routeStore.GetRoutesAsync(messageType, cancellation)).ToArray();
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(CancellationToken cancellation)
        {
            return (await _routeStore.GetRoutesAsync(cancellation)).ToArray();
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing RouteStoreSkeleton");

            using (_lock.Lock())
            {
                foreach (var endPoints in _activeRoutes.ToList())
                {
                    RemoveRouteAsync(endPoints, cancellation: default).GetAwaiter().GetResult(); // TODO
                }
            }
        }
    }
}
