using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Routing;
using AI4E.Utils;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity
{
    // TODO: This thing is currently not thread safe in regards to the consistency of the coordination service's memory.
    //       We can only ensure consistency within a single session here, but do we have to do?
    public sealed class RouteManager : IRouteManager
    {
        private static readonly byte[] _emptyPayload = new byte[0];
        private static readonly CoordinationEntryPath _routesRootPath = new CoordinationEntryPath("routes");
        private static readonly CoordinationEntryPath _reverseRoutesRootPath = new CoordinationEntryPath("reverse-routes");

        private readonly ICoordinationManager _coordinationManager;

        public RouteManager(ICoordinationManager coordinationManager)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            _coordinationManager = coordinationManager;
        }

        #region IRouteStore

        public async Task AddRouteAsync(EndPointAddress endPoint, RouteRegistration routeRegistration, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (routeRegistration == default)
                throw new ArgumentDefaultException(nameof(routeRegistration));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var entryCreationMode = EntryCreationModes.Default;

            if (routeRegistration.RegistrationOptions.IncludesFlag(RouteRegistrationOptions.Transient))
            {
                entryCreationMode |= EntryCreationModes.Ephemeral;
            }

            var reversePath = GetReversePath(session, endPoint, routeRegistration.Route);

            using (var stream = new MemoryStream(capacity: 4))
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((int)routeRegistration.RegistrationOptions);
                }
                var payload = stream.ToArray();
                await _coordinationManager.CreateAsync(reversePath, payload, entryCreationMode, cancellation);
            }

            var path = GetPath(routeRegistration.Route, endPoint, session);

            using (var stream = new MemoryStream(capacity: 4 + 4 + endPoint.Utf8EncodedValue.Length))
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((int)routeRegistration.RegistrationOptions);
                    writer.Write(endPoint);
                }
                var payload = stream.ToArray();
                // TODO: What to do if the entry already existed but with different options?
                await _coordinationManager.GetOrCreateAsync(path, payload, entryCreationMode, cancellation);
            }
        }

        public async Task RemoveRouteAsync(EndPointAddress endPoint, Route route, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(route, endPoint, session);
            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);

            var reversePath = GetReversePath(session, endPoint, route);
            await _coordinationManager.DeleteAsync(reversePath, cancellation: cancellation);
        }

        public async Task RemoveRoutesAsync(EndPointAddress endPoint, bool removePersistentRoutes, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetReversePath(session, endPoint);
            var reverseEntry = await _coordinationManager.GetAsync(path, cancellation);

            if (reverseEntry == null)
                return;

            var tasks = new List<Task>(capacity: reverseEntry.Children.Count);

            foreach (var reverseRouteEntry in await reverseEntry.GetChildrenEntriesAsync(cancellation))
            {
                var route = new Route(reverseRouteEntry.Name.Segment.ConvertToString());
                var routePath = GetPath(route, endPoint, session);

                if (!removePersistentRoutes)
                {
                    using (var stream = reverseRouteEntry.OpenStream())
                    using (var reader = new BinaryReader(stream))
                    {
                        var registrationOptions = (RouteRegistrationOptions)reader.ReadInt32();

                        if (!registrationOptions.IncludesFlag(RouteRegistrationOptions.Transient))
                        {
                            continue;
                        }

                        var reverseRouteEntryDeletion = _coordinationManager.DeleteAsync(reverseRouteEntry.Path, recursive: true, cancellation: cancellation);
                        tasks.Add(reverseRouteEntryDeletion.AsTask());
                    }
                }

                var routeEntryDeletion = _coordinationManager.DeleteAsync(routePath, cancellation: cancellation);
                tasks.Add(routeEntryDeletion.AsTask());
            }

            await Task.WhenAll(tasks);

            if (removePersistentRoutes)
            {
                await _coordinationManager.DeleteAsync(path, recursive: true, cancellation: cancellation);
            }
        }

        public async Task<IEnumerable<RouteTarget>> GetRoutesAsync(Route route, CancellationToken cancellation)
        {
            var path = GetPath(route);
            var entry = await _coordinationManager.GetOrCreateAsync(path, _emptyPayload, EntryCreationModes.Default, cancellation);

            Assert(entry != null);

            RouteTarget Extract(IEntry e)
            {
                using (var stream = e.OpenStream())
                using (var reader = new BinaryReader(stream))
                {
                    var registrationOptions = (RouteRegistrationOptions)reader.ReadInt32();
                    var endPoint = reader.ReadEndPointAddress();

                    return new RouteTarget(endPoint, registrationOptions);
                }
            }

            return await entry.GetChildrenEntries()
                              .Select(p => Extract(p))
                              .Distinct(p => p.EndPoint) // TODO: Can we do anything about the case, that an end-point is registered with different options for the route?
                              .ToArray();
        }

        #endregion

        private static CoordinationEntryPath GetPath(Route route)
        {
            return _routesRootPath.GetChildPath(route.ToString());
        }

        private static CoordinationEntryPath GetPath(Route route, EndPointAddress endPoint, string session)
        {
            var uniqueEntryName = IdGenerator.GenerateId(endPoint.ToString(), session);
            return _routesRootPath.GetChildPath(route.ToString(), uniqueEntryName);
        }

        private static CoordinationEntryPath GetReversePath(string session, EndPointAddress endPoint)
        {
            return _reverseRoutesRootPath.GetChildPath(session, endPoint.ToString());
        }

        private static CoordinationEntryPath GetReversePath(string session, EndPointAddress endPoint, Route route)
        {
            return _reverseRoutesRootPath.GetChildPath(session, endPoint.ToString(), route.ToString());
        }
    }

    public sealed class RouteManagerFactory : IRouteManagerFactory
    {
        private readonly ICoordinationManager _coordinationManager;

        public RouteManagerFactory(ICoordinationManager coordinationManager)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            _coordinationManager = coordinationManager;
        }

        public IRouteManager CreateRouteManager()
        {
            return new RouteManager(_coordinationManager);
        }
    }
}
