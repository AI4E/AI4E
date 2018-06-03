using AI4E.Routing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI4E.SignalR.Server.Abstractions
{
    public interface IClientRemoteMessageDispatcherAssociationStorage
    {
        void AddAssociation(string connectionId, Func<string, IRemoteMessageDispatcher> factory);
        void RemoveAssociation(string connectionId);
        IMessageDispatcher GetMessageDispatcher(string connectionId);
    }
}
