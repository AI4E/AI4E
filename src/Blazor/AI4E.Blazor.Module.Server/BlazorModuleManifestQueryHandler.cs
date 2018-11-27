using System;
using AI4E.Blazor.Modularity;

namespace AI4E.Blazor.Server
{
    [MessageHandler]
    internal sealed class BlazorModuleManifestQueryHandler
    {
        private readonly IBlazorModuleManifestProvider _manifestProvider;

        public BlazorModuleManifestQueryHandler(IBlazorModuleManifestProvider manifestProvider)
        {
            if (manifestProvider == null)
                throw new ArgumentNullException(nameof(manifestProvider));

            _manifestProvider = manifestProvider;
        }

        public BlazorModuleManifest Handle(Query<BlazorModuleManifest> query)
        {
            return _manifestProvider.GetBlazorModuleManifest();
        }
    }
}
