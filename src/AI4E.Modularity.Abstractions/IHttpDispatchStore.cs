using System.Threading;
using System.Threading.Tasks;
using AI4E.Routing;

namespace AI4E.Modularity
{
    public interface IHttpDispatchStore // TODO: Rename
    {
        Task AddRouteAsync(EndPointRoute localEndPoint, string prefix, CancellationToken cancellation);
        Task<EndPointRoute> GetRouteAsync(string path, CancellationToken cancellation);
        Task RemoveRouteAsync(EndPointRoute localEndPoint, string prefix, CancellationToken cancellation);
    }
}