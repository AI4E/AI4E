using System.Collections.Generic;

namespace AI4E.Messaging.Remote
{
    public interface IRouteEndPointScheduler<TAddress>
    {
        IEnumerable<TAddress> Schedule(IEnumerable<TAddress> replica);
    }
}
