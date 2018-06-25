using System.Collections.Generic;
using AI4E.Internal;

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
