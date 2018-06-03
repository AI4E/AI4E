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
            _dictionary = new ConcurrentDictionary<string, IRemoteMessageDispatcher>();
        }

        public ConcurrentDictionary<string, IRemoteMessageDispatcher> _dictionary { get; private set; }


        public void AddAssociation(string connectionId, Func<string, IRemoteMessageDispatcher> factory)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("must not be null or empty", nameof(connectionId));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _dictionary.GetOrAdd(connectionId, factory);
        }

        public IMessageDispatcher GetMessageDispatcher(string connectionId)
        {
            if (connectionId == null)
            {
                throw new ArgumentNullException(nameof(connectionId));
            }

            IRemoteMessageDispatcher dispatcher;

            if(_dictionary.TryGetValue(connectionId, out dispatcher))
            {
                return dispatcher;
            }
            else
            {
                return null;
            }
        }

        public void RemoveAssociation(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException("must not be null or empty", nameof(connectionId));
            }

            _dictionary.TryRemove(connectionId, out IRemoteMessageDispatcher value);
        }
    }
}
