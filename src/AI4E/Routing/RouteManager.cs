using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class RouteManager : IRouteStore
    {
        private const string _routesRootPath = "/routes";
        private const string _seperatorString = "->";

        private readonly ICoordinationManager _coordinationManager;

        public RouteManager(ICoordinationManager coordinationManager)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            _coordinationManager = coordinationManager;
        }

        #region IRouteStore

        public async Task AddRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));

            var route = localEndPoint.Route;
            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var path = GetPath(messageType, route, session);

            await _coordinationManager.GetOrCreateAsync(path, new byte[0], EntryCreationModes.Ephemeral, cancellation);
        }

        public async Task RemoveRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));

            var route = localEndPoint.Route;
            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var path = GetPath(messageType, route, session);

            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentNullOrWhiteSpaceException(nameof(messageType));

            var path = GetPath(messageType);
            var entry = await _coordinationManager.GetOrCreateAsync(path, new byte[0], EntryCreationModes.Default, cancellation);

            Assert(entry != null);

            return await entry.Children.Select(p => EndPointRoute.CreateRoute(ExtractRoute(p.Path))).Distinct().ToArray();
        }

        #endregion

        private static string GetPath(string messageType)
        {
            var messageTypeB = new StringBuilder(messageType.Length + EscapeHelper.CountCharsToEscape(messageType));
            messageTypeB.Append(messageType);
            EscapeHelper.Escape(messageTypeB, startIndex: 0);

            return EntryPathHelper.GetChildPath(_routesRootPath, messageType.ToString(), normalize: false);
        }

        private static string GetPath(string messageType, string route, string session)
        {
            return EntryPathHelper.GetChildPath(GetPath(messageType), GetEntryName(route, session));
        }

        // Gets the entry name that is roughly {route}->{session}
        private static string GetEntryName(string route, string session)
        {
            var resultsBuilder = new StringBuilder(route.Length +
                                                   session.Length +
                                                   EscapeHelper.CountCharsToEscape(route) +
                                                   EscapeHelper.CountCharsToEscape(session) +
                                                   1);
            resultsBuilder.Append(route);

            EscapeHelper.Escape(resultsBuilder, 0);

            var sepIndex = resultsBuilder.Length;

            resultsBuilder.Append(' ');
            resultsBuilder.Append(' ');

            resultsBuilder.Append(session);

            EscapeHelper.Escape(resultsBuilder, sepIndex + 2);

            // We need to ensure that the created entry is unique. Append any char that is neither - nor / not \
            resultsBuilder[sepIndex] = _seperatorString[0];
            resultsBuilder[sepIndex + 1] = _seperatorString[1];

            return resultsBuilder.ToString();

        }

        private static string ExtractRoute(string path)
        {
            var index = path.IndexOf(_seperatorString);

            if (index == -1)
            {
                // TODO: Log warning
                return null;
            }

            var resultBuilder = new StringBuilder(path, startIndex: 0, length: index, capacity: index);

            EscapeHelper.Unescape(resultBuilder, startIndex: 0);

            return resultBuilder.ToString();
        }
    }
}
