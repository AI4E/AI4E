using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class RouteManager : IRouteStore
    {
        private static readonly byte[] _emptyPayload = new byte[0];
        private static readonly CoordinationEntryPath _routesRootPath = new CoordinationEntryPath("routes");

        private readonly ICoordinationManager _coordinationManager;
        private readonly RouteOptions _options;

        public RouteManager(ICoordinationManager coordinationManager, RouteOptions options)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            _coordinationManager = coordinationManager;
            _options = options;
        }

        #region IRouteStore

        public async Task AddRouteAsync(EndPointRoute endPoint, string messageType, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));

            var route = endPoint.Route;
            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var path = GetPath(messageType, route, session);

            var routeBytes = Encoding.UTF8.GetBytes(route);

            using (var stream = new MemoryStream(capacity: 4 + 4 + routeBytes.Length))
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((int)_options);
                    writer.Write(routeBytes.Length);
                    writer.Write(routeBytes);
                }
                var payload = stream.ToArray();
                await _coordinationManager.GetOrCreateAsync(path, payload, EntryCreationModes.Ephemeral, cancellation);
            }
        }

        public async Task RemoveRouteAsync(EndPointRoute endPoint, string messageType, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));

            var route = endPoint.Route;
            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var path = GetPath(messageType, route, session);

            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);
        }

        public async Task<IEnumerable<(EndPointRoute endPoint, RouteOptions options)>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));

            var path = GetPath(messageType);
            var entry = await _coordinationManager.GetOrCreateAsync(path, _emptyPayload, EntryCreationModes.Default, cancellation);

            Assert(entry != null);

            (EndPointRoute endPoint, RouteOptions options) Extract(IEntry e)
            {
                using (var stream = e.OpenStream())
                using (var reader = new BinaryReader(stream))
                {
                    var options = (RouteOptions)reader.ReadInt32();
                    var endPointBytesLength = reader.ReadInt32();
                    var endPointBytes = reader.ReadBytes(endPointBytesLength);
                    var endPoint = EndPointRoute.CreateRoute(Encoding.UTF8.GetString(endPointBytes));

                    return (endPoint, options);
                }
            }

            return await entry.GetChildrenEntries()
                              .Select(p => Extract(p))
                              .Distinct(p => p.endPoint)
                              .ToArray();
        }

        #endregion

        private static CoordinationEntryPath GetPath(string messageType)
        {
            return _routesRootPath.GetChildPath(messageType);
        }

        private static CoordinationEntryPath GetPath(string messageType, string route, string session)
        {
            var uniqueEntryName = IdGenerator.GenerateId(route, session);
            return _routesRootPath.GetChildPath(messageType, uniqueEntryName);
        }
    }

    public sealed class RouteManagerFactory : IRouteStoreFactory
    {
        private readonly ICoordinationManager _coordinationManager;

        public RouteManagerFactory(ICoordinationManager coordinationManager)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));
            _coordinationManager = coordinationManager;
        }

        public IRouteStore CreateRouteStore(RouteOptions options)
        {
            // TODO: Validate options

            return new RouteManager(_coordinationManager, options);
        }
    }
}
