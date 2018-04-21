using System;
using System.Collections.Generic;
using System.Text;

namespace AI4E.Routing
{
    public interface IEndPointScheduler<TAddress>
    {
        IEnumerable<TAddress> Schedule(IEnumerable<TAddress> replica);
    }
}
