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
        ConcurrentDictionary<string, IRemoteMessageDispatcher> ClientRemoteMessageDispatcherAssociations { get; }
        Task AddAssociationAsync(string connectionId, IRemoteMessageDispatcher remoteMessageDispatcher);
        Task RemoveAssociationAsync(string connectionId);
        Task<IMessageDispatcher> GetMessageDispatcherAsync(string connectionId);
    }
}
