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

        public async Task AddRouteAsync(EndPointAddress endPoint, string route, RouteRegistrationOptions registrationOptions, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentNullOrWhiteSpaceException(nameof(route));

            if (!registrationOptions.IsValid())
                throw new ArgumentException("Invalid enum value.", nameof(registrationOptions));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var entryCreationMode = EntryCreationModes.Default;

            if (registrationOptions.IncludesFlag(RouteRegistrationOptions.Transient))
            {
                entryCreationMode |= EntryCreationModes.Ephemeral;
            }

            var reversePath = GetReversePath(session, endPoint, route);

            using (var stream = new MemoryStream(capacity: 4))
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((int)registrationOptions);
                }
                var payload = stream.ToArray();
                await _coordinationManager.CreateAsync(reversePath, payload, entryCreationMode, cancellation);
            }

            var path = GetPath(route, endPoint, session);

            using (var stream = new MemoryStream(capacity: 4 + 4 + endPoint.Utf8EncodedValue.Length))
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write((int)registrationOptions);
                    writer.Write(endPoint);
                }
                var payload = stream.ToArray();
                // TODO: What to do if the entry already existed but with different options?
                await _coordinationManager.GetOrCreateAsync(path, payload, entryCreationMode, cancellation);
            }
        }

        public async Task RemoveRouteAsync(EndPointAddress endPoint, string route, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentNullOrWhiteSpaceException(nameof(route));

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
                var route = reverseRouteEntry.Name.Segment.ConvertToString();
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

        public async Task<IEnumerable<(EndPointAddress endPoint, RouteRegistrationOptions registrationOptions)>> GetRoutesAsync(string route, CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentNullOrWhiteSpaceException(nameof(route));

            var path = GetPath(route);
            var entry = await _coordinationManager.GetOrCreateAsync(path, _emptyPayload, EntryCreationModes.Default, cancellation);

            Assert(entry != null);

            (EndPointAddress endPoint, RouteRegistrationOptions registrationOptions) Extract(IEntry e)
            {
                using (var stream = e.OpenStream())
                using (var reader = new BinaryReader(stream))
                {
                    var registrationOptions = (RouteRegistrationOptions)reader.ReadInt32();
                    var endPoint = reader.ReadEndPointAddress();

                    return (endPoint, registrationOptions);
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

        private static CoordinationEntryPath GetPath(string route, EndPointAddress endPoint, string session)
        {
            var uniqueEntryName = IdGenerator.GenerateId(endPoint.ToString(), session);
            return _routesRootPath.GetChildPath(route, uniqueEntryName);
        }

        private static CoordinationEntryPath GetReversePath(string session, EndPointAddress endPoint)
        {
            return _reverseRoutesRootPath.GetChildPath(session, endPoint.ToString());
        }

        private static CoordinationEntryPath GetReversePath(string session, EndPointAddress endPoint, string route)
        {
            return _reverseRoutesRootPath.GetChildPath(session, endPoint.ToString(), route);
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
