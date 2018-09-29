using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Remoting;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class RouteMap<TAddress> : IRouteMap<TAddress>
    {
        private static readonly CoordinationEntryPath _mapsRootPath = new CoordinationEntryPath("maps");

        private readonly ICoordinationManager _coordinationManager;
        private readonly IAddressConversion<TAddress> _addressConversion;

        public RouteMap(ICoordinationManager coordinationManager, IAddressConversion<TAddress> addressConversion)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _coordinationManager = coordinationManager;
            _addressConversion = addressConversion;
        }

        #region IRouteMap<TAddress>

        public async Task MapRouteAsync(EndPointRoute localEndPoint, TAddress address, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var route = localEndPoint.Route;
            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(route, session);

            await _coordinationManager.GetOrCreateAsync(path, _addressConversion.SerializeAddress(address), EntryCreationModes.Ephemeral, cancellation);
        }

        public async Task UnmapRouteAsync(EndPointRoute localEndPoint, TAddress address, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var route = localEndPoint.Route;
            var routeEntry = await GetRouteEntryAsync(route, cancellation);
            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(route, session);

            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);
        }

        public async Task UnmapRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            var route = localEndPoint.Route;
            var path = GetPath(route);

            await _coordinationManager.DeleteAsync(path, recursive: true, cancellation: cancellation);
        }

        public async ValueTask<IEnumerable<TAddress>> GetMapsAsync(EndPointRoute endPoint, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            var route = endPoint.Route;
            var routeEntry = await GetRouteEntryAsync(route, cancellation);

            Assert(routeEntry != null);

            var entries = await routeEntry.GetChildrenEntries().ToArray(cancellation);

            return entries.Select(p => _addressConversion.DeserializeAddress(p.Value.ToArray()));
        }

        #endregion

        private ValueTask<IEntry> GetRouteEntryAsync(string route, CancellationToken cancellation)
        {
            var path = GetPath(route);
            return _coordinationManager.GetOrCreateAsync(path, ReadOnlyMemory<byte>.Empty, EntryCreationModes.Default, cancellation);
        }

        private static CoordinationEntryPath GetPath(string route)
        {
            return _mapsRootPath.GetChildPath(route);
        }

        private static CoordinationEntryPath GetPath(string route, string session)
        {
            return _mapsRootPath.GetChildPath(route, session);
        }
    }
}
