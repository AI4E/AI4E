using System.Collections.Generic;
using System.Linq;

namespace AI4E.Messaging.Remote
{
    public sealed class RouteEndPointScheduler<TAddress> : IRouteEndPointScheduler<TAddress>
    {
        public IEnumerable<TAddress> Schedule(IEnumerable<TAddress> replica)
        {
            return replica.Shuffle();
        }
    }
}
