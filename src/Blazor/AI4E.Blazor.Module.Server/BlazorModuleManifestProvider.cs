using System;
using AI4E.Blazor.Modularity;

namespace AI4E.Blazor.Server
{
    public sealed class BlazorModuleManifestProvider : IBlazorModuleManifestProvider
    {
        private readonly BlazorModuleManifest _manifest;

        public BlazorModuleManifestProvider(BlazorModuleManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            _manifest = manifest;
        }

        public BlazorModuleManifest GetBlazorModuleManifest()
        {
            return _manifest;
        }
    }
}
