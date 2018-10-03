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
    // TODO: This thing is currently not thread safe in regards to the consistency of the coordination service's memory.
    //       We can only ensure consistency within a single session here, but do we have to do?
    public sealed class RouteManager : IRouteStore
    {
        private static readonly byte[] _emptyPayload = new byte[0];
        private static readonly CoordinationEntryPath _routesRootPath = new CoordinationEntryPath("routes");
        private static readonly CoordinationEntryPath _reverseRoutesRootPath = new CoordinationEntryPath("reverse-routes");

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

        public async Task AddRouteAsync(EndPointRoute endPoint, string route, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentNullOrWhiteSpaceException(nameof(route));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var reversePath = GetReversePath(session, endPoint.Route, route);
            await _coordinationManager.CreateAsync(reversePath, ReadOnlyMemory<byte>.Empty, EntryCreationModes.Ephemeral, cancellation);

            var path = GetPath(route, endPoint.Route, session);
            var endPointBytes = Encoding.UTF8.GetBytes(endPoint.Route);

            using (var stream = new MemoryStream(capacity: 4 + 4 + endPointBytes.Length))
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((int)_options);
                    writer.Write(endPointBytes.Length);
                    writer.Write(endPointBytes);
                }
                var payload = stream.ToArray();
                await _coordinationManager.GetOrCreateAsync(path, payload, EntryCreationModes.Ephemeral, cancellation);
            }
        }

        public async Task RemoveRouteAsync(EndPointRoute endPoint, string route, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentNullOrWhiteSpaceException(nameof(route));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(route, endPoint.Route, session);
            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);

            var reversePath = GetReversePath(session, endPoint.Route, endPoint.Route);
            await _coordinationManager.DeleteAsync(reversePath, cancellation: cancellation);
        }

        public async Task RemoveRoutesAsync(EndPointRoute endPoint, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetReversePath(session, endPoint.Route);
            var entry = await _coordinationManager.GetAsync(path, cancellation);

            if (entry == null)
                return;

            var tasks = new List<Task>(capacity: entry.Children.Count);

            foreach (var routeEntry in await entry.GetChildrenEntriesAsync(cancellation))
            {
                var route = routeEntry.Name.Segment.ConvertToString();
                var routePath = GetPath(route, endPoint.Route, session);

                var deletion = _coordinationManager.DeleteAsync(routePath, cancellation: cancellation);

                tasks.Add(deletion.AsTask());
            }

            await Task.WhenAll(tasks);
            await _coordinationManager.DeleteAsync(path, recursive: true, cancellation: cancellation);
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

        private static CoordinationEntryPath GetPath(string route)
        {
            return _routesRootPath.GetChildPath(route);
        }

        private static CoordinationEntryPath GetPath(string route, string endPoint, string session)
        {
            var uniqueEntryName = IdGenerator.GenerateId(endPoint, session);
            return _routesRootPath.GetChildPath(route, uniqueEntryName);
        }

        private static CoordinationEntryPath GetReversePath(string session, string endPoint)
        {
            return _reverseRoutesRootPath.GetChildPath(session, endPoint);
        }

        private static CoordinationEntryPath GetReversePath(string session, string endPoint, string route)
        {
            return _reverseRoutesRootPath.GetChildPath(session, endPoint, route);
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
