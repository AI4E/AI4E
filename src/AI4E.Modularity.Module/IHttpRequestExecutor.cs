using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Module
{
    public interface IHttpRequestExecutor
    {
        ValueTask<ModuleHttpResponse> ExecuteAsync(ModuleHttpRequest request, CancellationToken cancellation);
    }
}
