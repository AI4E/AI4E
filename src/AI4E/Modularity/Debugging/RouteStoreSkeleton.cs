using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using Nito.AsyncEx;

namespace AI4E.Modularity.Debugging
{
    public sealed class RouteStoreSkeleton : IAsyncDisposable
    {
        private readonly IRouteStore _routeStore;
        private readonly HashSet<EndPointRoute> _activeRoutes = new HashSet<EndPointRoute>();
        private readonly AsyncLock _lock = new AsyncLock();

        public RouteStoreSkeleton(IRouteStore routeStore)
        {
            if (routeStore == null)
                throw new ArgumentNullException(nameof(routeStore));

            _routeStore = routeStore;
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
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

        public async Task RemoveRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            await _routeStore.RemoveRouteAsync(localEndPoint, cancellation);

            using (await _lock.LockAsync())
            {
                _activeRoutes.Remove(localEndPoint);
            }
        }

        public Task<bool> RemoveRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            return _routeStore.RemoveRouteAsync(localEndPoint, messageType, cancellation);
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            return (await _routeStore.GetRoutesAsync(messageType, cancellation)).ToArray();
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(CancellationToken cancellation)
        {
            return (await _routeStore.GetRoutesAsync(cancellation)).ToArray();
        }

        #region Disposal

        private readonly AsyncDisposeHelper _disposeHelper;

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
            Console.WriteLine("Disposing RouteStoreSkeleton");

            using (await _lock.LockAsync())
            {
                foreach (var endPoints in _activeRoutes.ToList())
                {
                    await RemoveRouteAsync(endPoints, cancellation: default);
                }
            }
        }

        #endregion
    }
}
