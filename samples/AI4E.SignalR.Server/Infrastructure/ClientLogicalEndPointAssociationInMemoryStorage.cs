using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Routing;
using AI4E.SignalR.Server.Abstractions;

namespace AI4E.SignalR.Server.Infrastructure
{
    public class ClientLogicalEndPointAssociationInMemoryStorage : IClientLogicalEndPointAssociationStorage
    {
        public ConcurrentDictionary<string, ILogicalEndPoint> ClientLogicalEndPointAssociations { get; private set; }

        public async Task AddAssociationAsync(string connectionId, ILogicalEndPoint logicalEndPoint)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("must not be null or empty", nameof(connectionId));
            }

            if (logicalEndPoint == null)
            {
                throw new ArgumentNullException(nameof(logicalEndPoint));
            }

            ClientLogicalEndPointAssociations.AddOrUpdate(connectionId, logicalEndPoint,
                (key, existingValue) =>
                {
                    return existingValue;
                });
        }

        public async Task RemoveAssociationAsync(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("must not be null or empty", nameof(connectionId));
            }

            ClientLogicalEndPointAssociations.TryRemove(connectionId, out ILogicalEndPoint value);
        }
    }
}
