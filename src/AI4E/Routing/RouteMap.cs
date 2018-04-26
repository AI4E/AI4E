using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Remoting;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class RouteMap<TAddress> : IRouteMap<TAddress>
    {
        private const string _mapsRootPath = "/maps";

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

        public async Task<IEnumerable<TAddress>> GetMapsAsync(EndPointRoute endPoint, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            var route = endPoint.Route;
            var routeEntry = await GetRouteEntryAsync(route, cancellation);

            Assert(routeEntry != null);

            var entries = routeEntry.Children;

            return await entries.Select(p => _addressConversion.DeserializeAddress(p.Value.ToArray())).ToArray(cancellation);
        }

        private Task<IEntry> GetRouteEntryAsync(string route, CancellationToken cancellation)
        {
            var path = GetPath(route);
            return _coordinationManager.GetOrCreateAsync(path, new byte[0], EntryCreationModes.Default, cancellation);
        }

        private static string GetPath(string route)
        {
            var escapedRoute = new StringBuilder(EscapeHelper.CountCharsToEscape(route) + route.Length);
            escapedRoute.Append(route);
            EscapeHelper.Escape(escapedRoute, 0);

            return EntryPathHelper.GetChildPath(_mapsRootPath, escapedRoute.ToString(), normalize: false);
        }

        private static string GetPath(string route, string session)
        {
            var escapedSession = new StringBuilder(EscapeHelper.CountCharsToEscape(session) + session.Length);
            escapedSession.Append(session);
            EscapeHelper.Escape(escapedSession, 0);

            return EntryPathHelper.GetChildPath(GetPath(route), escapedSession.ToString(), normalize: false);
        }

        // TODO: Remove leaseEnd as this is not needed any more.
        // TODO: Why does this return bool?
        public async Task<bool> MapRouteAsync(EndPointRoute localEndPoint, TAddress address, DateTime leaseEnd, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var route = localEndPoint.Route;
            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var path = GetPath(route, session);

            await _coordinationManager.GetOrCreateAsync(path, _addressConversion.SerializeAddress(address), EntryCreationModes.Ephemeral, cancellation);

            return true;
        }

        // TODO: Why does this return bool?
        public async Task<bool> UnmapRouteAsync(EndPointRoute localEndPoint, TAddress address, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var route = localEndPoint.Route;
            var routeEntry = await GetRouteEntryAsync(route, cancellation);
            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var path = GetPath(route, session);

            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);

            return true;
        }

        public async Task UnmapRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            var route = localEndPoint.Route;
            var path = GetPath(route);

            await _coordinationManager.DeleteAsync(path, recursive: true, cancellation: cancellation);
        }
    }
}
