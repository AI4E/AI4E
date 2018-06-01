using AI4E.Routing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI4E.SignalR.Server.Abstractions
{
    public interface IClientLogicalEndPointAssociationStorage
    {
        ConcurrentDictionary<string, ILogicalEndPoint> ClientLogicalEndPointAssociations { get; }

        Task AddAssociationAsync(string connectionId, ILogicalEndPoint logicalEndPoint);
        Task RemoveAssociationAsync(string connectionId);
    }
}
