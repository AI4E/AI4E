using AI4E.Routing;
using AI4E.SignalR.Server.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI4E.SignalR.Server.Infrastructure
{
    public class ClientRemoteMessageDispatcherAssociationInMemoryStorage : IClientRemoteMessageDispatcherAssociationStorage
    {
        public ClientRemoteMessageDispatcherAssociationInMemoryStorage()
        {
            ClientRemoteMessageDispatcherAssociations = new ConcurrentDictionary<string, IRemoteMessageDispatcher>();
        }

        public ConcurrentDictionary<string, IRemoteMessageDispatcher> ClientRemoteMessageDispatcherAssociations { get; private set; }


        public async Task AddAssociationAsync(string connectionId, IRemoteMessageDispatcher remoteMessageDispatcher)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("must not be null or empty", nameof(connectionId));
            }

            if (remoteMessageDispatcher == null)
            {
                throw new ArgumentNullException(nameof(remoteMessageDispatcher));
            }

            ClientRemoteMessageDispatcherAssociations.AddOrUpdate(connectionId, remoteMessageDispatcher,
                (key, existingValue) =>
                {
                    return existingValue;
                });
        }

        public async Task<IMessageDispatcher> GetMessageDispatcherAsync(string connectionId)
        {
            if (connectionId == null)
            {
                throw new ArgumentNullException(nameof(connectionId));
            }

            IRemoteMessageDispatcher dispatcher;

            if(ClientRemoteMessageDispatcherAssociations.TryGetValue(connectionId, out dispatcher))
            {
                return dispatcher;
            }
            else
            {
                return null;
            }
        }

        public async Task RemoveAssociationAsync(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("must not be null or empty", nameof(connectionId));
            }

            ClientRemoteMessageDispatcherAssociations.TryRemove(connectionId, out IRemoteMessageDispatcher value);
        }
    }
}
