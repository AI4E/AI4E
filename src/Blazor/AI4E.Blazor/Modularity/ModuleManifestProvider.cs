using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Modularity.Host;
using AI4E.Routing;
using Microsoft.Extensions.Logging;

namespace AI4E.Blazor.Modularity
{
    internal sealed class ModuleManifestProvider : IModuleManifestProvider
    {
        private readonly IModulePropertiesLookup _modulePropertiesLookup;
        private readonly IRemoteMessageDispatcher _messageDispatcher;
        private readonly ILogger<ModuleManifestProvider> _logger;
        private readonly ConcurrentDictionary<ModuleIdentifier, BlazorModuleManifest> _cache;

        public ModuleManifestProvider(IModulePropertiesLookup modulePropertiesLookup, IRemoteMessageDispatcher messageDispatcher, ILogger<ModuleManifestProvider> logger = null)
        {
            if (modulePropertiesLookup == null)
                throw new ArgumentNullException(nameof(modulePropertiesLookup));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _modulePropertiesLookup = modulePropertiesLookup;
            _messageDispatcher = messageDispatcher;
            _logger = logger;

            _cache = new ConcurrentDictionary<ModuleIdentifier, BlazorModuleManifest>();
        }

        public ValueTask<BlazorModuleManifest> GetModuleManifestAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            _logger.LogDebug($"Requesting manifest for module {module}.");

            if (_cache.TryGetValue(module, out var result))
            {
                _logger.LogTrace($"Successfully loaded manifest for module {module} from the cache.");
                return new ValueTask<BlazorModuleManifest>(result);
            }


            return GetModuleManifestCoreAsync(module, cancellation);
        }

        private async ValueTask<BlazorModuleManifest> GetModuleManifestCoreAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var endPoint = await GetEndPointAsync(module, cancellation) ?? throw new Exception($"Unable to load manifest for {module}."); // TODO

            var dispatchData = new DispatchDataDictionary<Query<BlazorModuleManifest>>(new Query<BlazorModuleManifest>());
            var queryResult = await _messageDispatcher.DispatchAsync(dispatchData, publish: false, endPoint, cancellation);

            if (!queryResult.IsSuccessWithResult<BlazorModuleManifest>(out var manifest))
            {
                throw new Exception($"Unable to load manifest for {module}."); // TODO
            }

            _cache.TryAdd(module, manifest);

            _logger.LogTrace($"Successfully loaded manifest for module {module}.");

            return manifest;
        }

        private ValueTask<EndPointAddress?> GetEndPointAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            return _modulePropertiesLookup.LookupEndPointAsync(module, cancellation);
        }
    }
}
