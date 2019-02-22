using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    internal interface IModuleManifestProvider
    {
        ValueTask<BlazorModuleManifest> GetModuleManifestAsync(ModuleIdentifier module, bool bypassCache, CancellationToken cancellation = default);
    }

    internal static class ModuleManifestProviderExtension
    {
        public static ValueTask<BlazorModuleManifest> GetModuleManifestAsync(
            this IModuleManifestProvider moduleManifestProvider,
            ModuleIdentifier module,
            CancellationToken cancellation = default)
        {
            return moduleManifestProvider.GetModuleManifestAsync(module, bypassCache: false, cancellation);
        }
    }
}
