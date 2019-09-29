using System.Collections.Generic;
using System.Linq;

namespace AI4E.Routing
{
    public sealed class RandomEndPointScheduler<TAddress> : IEndPointScheduler<TAddress>
    {
        public IEnumerable<TAddress> Schedule(IEnumerable<TAddress> replica)
        {
            return replica.Shuffle();
        }
    }
}
