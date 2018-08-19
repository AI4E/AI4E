/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Routing;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity
{
    [Obsolete("Use RunningModuleLookup")]
    public sealed class HttpDispatchStore : IHttpDispatchStore // TODO: Rename
    {
        private static readonly byte[] _emptyPayload = new byte[0];
        private static readonly char[] _pathSeperators = { '/', '\\' };
        private const string _routesRootPath = "/http-routes";
        private const string _seperatorString = "->";

        private readonly ICoordinationManager _coordinationManager;

        public HttpDispatchStore(ICoordinationManager coordinationManager)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            _coordinationManager = coordinationManager;
        }

        public async Task AddRouteAsync(EndPointRoute localEndPoint, string prefix, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentNullOrWhiteSpaceException(nameof(prefix));

            var normalizedPrefix = GetNormalizedPrefix(prefix);

            // It is not possible to register a route for the root path.
            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                throw new ArgumentException("It is not possible to register a route for the root path.");
            }

            var route = localEndPoint.Route;
            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var path = GetPath(normalizedPrefix, route, session, normalize: false);

            await _coordinationManager.GetOrCreateAsync(path, _emptyPayload, EntryCreationModes.Ephemeral, cancellation);
        }

        public async Task RemoveRouteAsync(EndPointRoute localEndPoint, string prefix, CancellationToken cancellation)
        {
            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentNullOrWhiteSpaceException(nameof(prefix));

            var normalizedPrefix = GetNormalizedPrefix(prefix);

            // It is not possible to register a route for the root path.
            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                return;
            }

            var route = localEndPoint.Route;
            var session = await _coordinationManager.GetSessionAsync(cancellation);
            var path = GetPath(normalizedPrefix, route, session, normalize: false);

            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);
        }

        public async Task<EndPointRoute> GetRouteAsync(string path, CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullOrWhiteSpaceException(nameof(path));

            for (var a = new StringBuilder(path); a.Length > 0; a.Length = a.LastIndexOf('/', startIndex: 0, ignoreCase: true))
            {
                // TODO: Performance optimization: We are working with a string, turning it into a stringbuilder, than a string, a stringbuilder...
                var route = await GetRouteInternalAsync(a.ToString(), cancellation);

                if (route != null)
                {
                    return route;
                }
            }

            return null;
        }

        private async Task<EndPointRoute> GetRouteInternalAsync(string prefix, CancellationToken cancellation)
        {
            Assert(!string.IsNullOrWhiteSpace(prefix));

            var normalizedPrefix = GetNormalizedPrefix(prefix);

            // It is not possible to register a route for the root path.
            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                return null;
            }

            var path = GetPath(normalizedPrefix, normalize: false);
            var entry = await _coordinationManager.GetOrCreateAsync(path, _emptyPayload, EntryCreationModes.Default, cancellation);

            Assert(entry != null);

            // We take the entry that was registered first. This is done in order that a vitual end-point cannot override a route for an already existing end-point.
            var e = await entry.Childs.OrderBy(p => p.CreationTime).FirstOrDefault(cancellation);

            if (e != null)
            {
                return EndPointRoute.CreateRoute(ExtractRoute(e.Path));
            }

            return null;
        }

        private static string GetNormalizedPrefix(string prefix)
        {
            prefix = Regex.Replace(prefix, @"\s+", "");

            if (prefix.StartsWith("/"))
            {
                prefix = prefix.Substring(1);
            }

            return prefix;
        }

        private static string GetPath(string prefix, bool normalize = true)
        {
            if (normalize)
                prefix = GetNormalizedPrefix(prefix);

            var escapedPrefixBuilder = new StringBuilder(prefix.Length + EscapeHelper.CountCharsToEscape(prefix));
            escapedPrefixBuilder.Append(prefix);
            EscapeHelper.Escape(escapedPrefixBuilder, startIndex: 0);
            var escapedPrefix = escapedPrefixBuilder.ToString();

            return EntryPathHelper.GetChildPath(_routesRootPath, escapedPrefix, normalize: false);
        }

        private static string GetPath(string prefix, string route, string session, bool normalize = true)
        {
            return EntryPathHelper.GetChildPath(GetPath(prefix, normalize), GetEntryName(route, session));
        }

        // Gets the entry name that is roughly {route}->{session}
        // TODO: This is a copy of RouteManager.GetEntryName; Provide a way to get a session postfixed name in the coordination infrastructure itself.
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

            // We need to ensure that the created entry is unique.
            resultsBuilder[sepIndex] = _seperatorString[0];
            resultsBuilder[sepIndex + 1] = _seperatorString[1];

            return resultsBuilder.ToString();
        }

        // TODO: This is a copy of RouteManager.ExtractRoute; Provide a way to get a session postfixed name in the coordination infrastructure itself.
        private static string ExtractRoute(string path)
        {
            var nameIndex = path.LastIndexOfAny(_pathSeperators);
            var index = path.IndexOf(_seperatorString);

            if (index == -1)
            {
                // TODO: Log warning
                return null;
            }

            var resultBuilder = new StringBuilder(path, startIndex: nameIndex + 1, length: index - nameIndex - 1, capacity: index);

            EscapeHelper.Unescape(resultBuilder, startIndex: 0);

            return resultBuilder.ToString();
        }
    }
}
