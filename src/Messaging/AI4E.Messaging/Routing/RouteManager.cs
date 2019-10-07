using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Messaging.Routing
{
    public sealed class RouteManager : IRouteManager
    {
        private readonly Dictionary<Route, RouteManagerEntry> _entries = new Dictionary<Route, RouteManagerEntry>();
        private readonly object _lock = new object();

        public Task AddRouteAsync(
            RouteEndPointAddress endPoint,
            RouteRegistration routeRegistration,
            CancellationToken cancellation = default)
        {
            RouteManagerEntry entry;

            lock (_lock)
            {
                if (!_entries.TryGetValue(routeRegistration.Route, out entry))
                {
                    entry = new RouteManagerEntry();
                    _entries.Add(routeRegistration.Route, entry);
                }
            }

            entry.AddRoute(new RouteTarget(endPoint, routeRegistration.RegistrationOptions));

            return Task.CompletedTask;

        }

        public Task RemoveRouteAsync(
            RouteEndPointAddress endPoint,
            Route route,
            CancellationToken cancellation = default)
        {
            RouteManagerEntry entry;

            lock (_lock)
            {
                if (!_entries.TryGetValue(route, out entry))
                {
                    return Task.CompletedTask;
                }
            }

            entry.RemoveRoute(endPoint);
            return Task.CompletedTask;
        }

        public Task RemoveRoutesAsync(
            RouteEndPointAddress endPoint,
            bool removePersistentRoutes,
            CancellationToken cancellation = default)
        {
            ImmutableList<RouteManagerEntry> entries;

            lock (_lock)
            {
                entries = _entries.Values.ToImmutableList();
            }

            foreach (var entry in entries)
            {
                entry.RemoveRoute(endPoint);
            }

            return Task.CompletedTask;
        }

        public IAsyncEnumerable<RouteTarget> GetRoutesAsync(
            Route route,
            CancellationToken cancellation = default)
        {
            RouteManagerEntry entry;

            lock (_lock)
            {
                if (!_entries.TryGetValue(route, out entry))
                {
                    return AsyncEnumerable.Empty<RouteTarget>();
                }
            }

            return entry.Targets.ToAsyncEnumerable();
        }

        private sealed class RouteManagerEntry
        {
            private volatile ImmutableList<RouteTarget> _targets = ImmutableList<RouteTarget>.Empty;

            public RouteManagerEntry() { }

            public void AddRoute(RouteTarget target)
            {
                ImmutableList<RouteTarget> current = _targets, // Volatile read op.
                                           start,
                                           desired;

                do
                {
                    start = current;

                    if (start.Any(p => p.EndPoint == target.EndPoint))
                    {
                        return; // TODO: The registration options may have changed. What can we do here?
                    }

                    desired = start.Add(target);
                    current = Interlocked.CompareExchange(ref _targets, desired, start);
                }
                while (start != current);
            }

            public void RemoveRoute(RouteEndPointAddress endPoint)
            {
                ImmutableList<RouteTarget> current = _targets, // Volatile read op.
                                           start,
                                           desired;

                do
                {
                    start = current;

                    var i = 0;

                    for (; i < start.Count; i++)
                    {
                        if (start[i].EndPoint == endPoint)
                        {
                            break;
                        }
                    }

                    if (i == start.Count)
                    {
                        return;
                    }

                    desired = start.RemoveAt(i);
                    current = Interlocked.CompareExchange(ref _targets, desired, start);
                }
                while (start != current);
            }

            public ImmutableList<RouteTarget> Targets => _targets;
        }
    }
}
