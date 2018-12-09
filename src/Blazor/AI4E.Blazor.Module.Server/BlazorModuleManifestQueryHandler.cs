using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Blazor.Modularity;

namespace AI4E.Blazor.Module.Server
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

        public ValueTask<BlazorModuleManifest> HandleAsync(Query<BlazorModuleManifest> query, CancellationToken cancellation)
        {
            return _manifestProvider.GetBlazorModuleManifestAsync(cancellation);
        }
    }
}
