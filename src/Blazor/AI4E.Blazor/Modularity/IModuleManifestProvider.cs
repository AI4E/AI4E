using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    internal interface IModuleManifestProvider
    {
        ValueTask<BlazorModuleManifest> GetModuleManifestAsync(ModuleIdentifier module, CancellationToken cancellation);
    }
}
