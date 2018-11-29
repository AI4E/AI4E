using System.Threading;
using System.Threading.Tasks;
using AI4E.Blazor.Modularity;

namespace AI4E.Blazor.Server
{
    public interface IBlazorModuleManifestProvider
    {
        ValueTask<BlazorModuleManifest> GetBlazorModuleManifestAsync(CancellationToken cancellation);
    }
}
