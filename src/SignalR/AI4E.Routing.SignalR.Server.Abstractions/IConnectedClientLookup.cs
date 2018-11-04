using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Routing.SignalR.Server
{
    public interface IConnectedClientLookup
    {
        Task<(EndPointAddress endPoint, string securityToken)> AddClientAsync(CancellationToken cancellation);
        Task<bool> ValidateClientAsync(EndPointAddress endPoint, string securityToken, CancellationToken cancellation);

        event EventHandler<ClientsDisconnectedEventArgs> ClientsDisconnected;
    }

    public sealed class ClientsDisconnectedEventArgs : EventArgs
    {
        public ClientsDisconnectedEventArgs(IReadOnlyList<EndPointAddress> clients)
        {
            if (clients == null)
                throw new ArgumentNullException(nameof(clients));
            Clients = clients;
        }

        public IReadOnlyList<EndPointAddress> Clients { get; }
    }
}
