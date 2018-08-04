using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Routing.FrontEnd
{
    public interface IConnectedClientLookup
    {
        Task<(EndPointRoute endPoint, string securityToken)> AddClientAsync(CancellationToken cancellation);
        Task<bool> ValidateClientAsync(EndPointRoute endPoint, string securityToken, CancellationToken cancellation);
    }
}
