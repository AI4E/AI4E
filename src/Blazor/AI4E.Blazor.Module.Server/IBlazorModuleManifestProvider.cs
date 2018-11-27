using AI4E.Blazor.Modularity;

namespace AI4E.Blazor.Server
{
    public interface IBlazorModuleManifestProvider
    {
        BlazorModuleManifest GetBlazorModuleManifest();
    }
}
