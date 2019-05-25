using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Modularity.Host;
using AI4E.Routing;
using Microsoft.Extensions.Logging;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class ModuleManifestProvider : IModuleManifestProvider
    {
        private readonly IModulePropertiesLookup _modulePropertiesLookup;
        private readonly IRemoteMessageDispatcher _messageDispatcher;
        private readonly ILogger<ModuleManifestProvider> _logger;
        private readonly ConcurrentDictionary<ModuleIdentifier, BlazorModuleManifest> _cache;

        public ModuleManifestProvider(
            IModulePropertiesLookup modulePropertiesLookup,
            IRemoteMessageDispatcher messageDispatcher,
            ILogger<ModuleManifestProvider> logger = null)
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

        public ValueTask<BlazorModuleManifest> GetModuleManifestAsync(ModuleIdentifier module, bool bypassCache, CancellationToken cancellation)
        {
            _logger?.LogDebug($"Requesting manifest for module {module}.");

            if (!bypassCache && _cache.TryGetValue(module, out var result))
            {
                _logger?.LogTrace($"Successfully loaded manifest for module {module} from cache.");
                return new ValueTask<BlazorModuleManifest>(result);
            }

            return GetModuleManifestCoreAsync(module, cancellation);
        }

        private async ValueTask<BlazorModuleManifest> GetModuleManifestCoreAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var moduleProperties = await _modulePropertiesLookup.LookupAsync(module, cancellation);

            if (moduleProperties == null)
            {
                _logger?.LogError($"Unable to load manifest for {module}. The module properties could not be fetched.");
                return null;
            }

            foreach (var endPoint in moduleProperties.EndPoints)
            {
                var dispatchData = new DispatchDataDictionary<Query<BlazorModuleManifest>>(new Query<BlazorModuleManifest>());
                var queryResult = await _messageDispatcher.DispatchAsync(dispatchData, publish: false, endPoint, cancellation);

                if (!queryResult.IsSuccessWithResult<BlazorModuleManifest>(out var manifest))
                {
                    _logger?.LogWarning($"Unable to load manifest for {module} from end-point {endPoint}.");

                    continue;
                }

                _cache.TryAdd(module, manifest);
                _logger?.LogDebug($"Successfully loaded manifest for module {module}.");
                return manifest;
            }

            if (moduleProperties.EndPoints.Any())
            {
                _logger?.LogError($"Unable to load manifest for {module}. No end-point matched.");
            }
            else
            {
                _logger?.LogError($"Unable to load manifest for {module}. No end-points available.");
            }

            return null;
        }
    }
}
