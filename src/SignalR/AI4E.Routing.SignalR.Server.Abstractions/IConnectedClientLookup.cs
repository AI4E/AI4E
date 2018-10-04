using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Routing.SignalR.Server
{
    public interface IConnectedClientLookup
    {
        Task<(EndPointAddress endPoint, string securityToken)> AddClientAsync(CancellationToken cancellation);
        Task<bool> ValidateClientAsync(EndPointAddress endPoint, string securityToken, CancellationToken cancellation);
    }
}
