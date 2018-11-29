using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Routing;
using Microsoft.Extensions.Logging;

namespace AI4E.Blazor.Modularity
{
    internal sealed class ModuleManifestProvider : IModuleManifestProvider
    {
        private readonly IModulePrefixLookup _modulePrefixLookup;
        private readonly IRemoteMessageDispatcher _messageDispatcher;
        private readonly ILogger<ModuleManifestProvider> _logger;
        private readonly ConcurrentDictionary<ModuleIdentifier, BlazorModuleManifest> _manifestCache;

        public ModuleManifestProvider(IModulePrefixLookup modulePrefixLookup, IRemoteMessageDispatcher messageDispatcher, ILogger<ModuleManifestProvider> logger = null)
        {
            if (modulePrefixLookup == null)
                throw new ArgumentNullException(nameof(modulePrefixLookup));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _modulePrefixLookup = modulePrefixLookup;
            _messageDispatcher = messageDispatcher;
            _logger = logger;

            _manifestCache = new ConcurrentDictionary<ModuleIdentifier, BlazorModuleManifest>();
        }

        public ValueTask<BlazorModuleManifest> GetModuleManifestAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            _logger.LogDebug($"Requesting manifest for module {module}.");

            if (_manifestCache.TryGetValue(module, out var result))
            {
                _logger.LogTrace($"Successfully loaded manifest for module {module} from the cache.");
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
                throw new Exception($"Unable to load manifest for {module}."); // TODO
            }

            _manifestCache.TryAdd(module, manifest);

            _logger.LogTrace($"Successfully loaded manifest for module {module}.");

            return manifest;
        }

        private ValueTask<EndPointAddress> GetEndPointAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            return _modulePrefixLookup.LookupEndPointAsync(module, cancellation);
        }
    }
}
