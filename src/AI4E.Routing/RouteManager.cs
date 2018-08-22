using System;
using System.Collections.Generic;
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
        private static readonly char[] _pathSeperators = { '/', '\\' };
        private const string _routesRootPath = "/routes";
        private const string _seperatorString = "->";

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

            var payload = BitConverter.GetBytes((int)_options);

            await _coordinationManager.GetOrCreateAsync(path, payload, EntryCreationModes.Ephemeral, cancellation);
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
            var entry = await _coordinationManager.GetOrCreateAsync(path, new byte[0], EntryCreationModes.Default, cancellation);

            Assert(entry != null);

            (EndPointRoute endPoint, RouteOptions options) Extract(IEntry e)
            {
                var endPoint = EndPointRoute.CreateRoute(EntryPathHelper.ExtractRoute(e.Path));
                var options = (RouteOptions)BitConverter.ToInt32(e.Value.ToArray(), 0);

                return (endPoint, options);
            }

            return await entry.Childs
                              .Select(p => Extract(p))
                              .Distinct(p => p.endPoint)
                              .ToArray();
        }

        #endregion

        private static string GetPath(string messageType)
        {
            var escapedMessageTypeBuilder = new StringBuilder(messageType.Length + EscapeHelper.CountCharsToEscape(messageType));
            escapedMessageTypeBuilder.Append(messageType);
            EscapeHelper.Escape(escapedMessageTypeBuilder, startIndex: 0);
            var escapedMessageType = escapedMessageTypeBuilder.ToString();

            return EntryPathHelper.GetChildPath(_routesRootPath, escapedMessageType, normalize: false);
        }

        private static string GetPath(string messageType, string route, string session)
        {
            return EntryPathHelper.GetChildPath(GetPath(messageType), EntryPathHelper.GetEntryName(route, session));
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
