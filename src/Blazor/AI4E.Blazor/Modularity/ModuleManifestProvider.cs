using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Routing;

namespace AI4E.Blazor.Modularity
{
    internal sealed class ModuleManifestProvider : IModuleManifestProvider
    {
        private readonly IModulePrefixLookup _modulePrefixLookup;
        private readonly IRemoteMessageDispatcher _messageDispatcher;

        private readonly ConcurrentDictionary<ModuleIdentifier, BlazorModuleManifest> _manifestCache;

        public ModuleManifestProvider(IModulePrefixLookup modulePrefixLookup, IRemoteMessageDispatcher messageDispatcher)
        {
            if (modulePrefixLookup == null)
                throw new ArgumentNullException(nameof(modulePrefixLookup));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _modulePrefixLookup = modulePrefixLookup;
            _messageDispatcher = messageDispatcher;

            _manifestCache = new ConcurrentDictionary<ModuleIdentifier, BlazorModuleManifest>();
        }

        public ValueTask<BlazorModuleManifest> GetModuleManifestAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (_manifestCache.TryGetValue(module, out var result))
            {
                return new ValueTask<BlazorModuleManifest>(result);
            }

            return GetModuleManifestCoreAsync(module, cancellation);
        }

        private async ValueTask<BlazorModuleManifest> GetModuleManifestCoreAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var endPoint = await GetEndPointAsync(module, cancellation);

            var dispatchData = new DispatchDataDictionary<Query<BlazorModuleManifest>>(new Query<BlazorModuleManifest>());
            var queryResult = await _messageDispatcher.DispatchAsync(dispatchData, publish: false, endPoint, cancellation);

            if (!queryResult.IsSuccessWithResult<BlazorModuleManifest>(out var manifest))
            {
                throw new Exception(); // TODO
            }

            _manifestCache.TryAdd(module, manifest);

            return manifest;
        }

        private ValueTask<EndPointAddress> GetEndPointAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            return _modulePrefixLookup.LookupEndPointAsync(module, cancellation);
        }
    }
}
