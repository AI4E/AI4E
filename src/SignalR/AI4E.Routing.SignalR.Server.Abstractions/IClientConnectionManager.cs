using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Routing.SignalR.Server
{
    public interface IClientConnectionManager
    {
        TimeSpan Timeout { get; }

        Task<(EndPointAddress endPoint, string securityToken)> AddClientAsync(CancellationToken cancellation = default);
        Task<bool> ValidateClientAsync(EndPointAddress endPoint, string securityToken, CancellationToken cancellation = default);

        Task WaitForDisconnectAsync(EndPointAddress endPoint, CancellationToken cancellation = default);
    }
}
