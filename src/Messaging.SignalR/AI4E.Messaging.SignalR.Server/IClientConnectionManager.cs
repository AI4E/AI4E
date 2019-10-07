using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;

namespace AI4E.Messaging.SignalR.Server
{
    public interface IClientConnectionManager : IDisposable
    {
        TimeSpan Timeout { get; }

        ValueTask<ClientCredentials> AddClientAsync(CancellationToken cancellation = default);

        ValueTask<bool> ValidateClientAsync(
            ClientCredentials credentials,
            CancellationToken cancellation = default);

        ValueTask WaitForDisconnectAsync(RouteEndPointAddress endPoint, CancellationToken cancellation = default);
    }

    public readonly struct ClientCredentials
    {
        public ClientCredentials(RouteEndPointAddress endPoint, string securityToken)
        {
            EndPoint = endPoint;
            SecurityToken = securityToken;
        }

        public RouteEndPointAddress EndPoint { get; }
        public string SecurityToken { get; }
    }
}
