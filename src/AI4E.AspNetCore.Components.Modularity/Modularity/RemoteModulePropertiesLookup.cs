using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Modularity.Host;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Blazor.Modularity
{
    public sealed class RemoteModulePropertiesLookup : IModulePropertiesLookup
    {
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly ILogger<RemoteModulePropertiesLookup> _logger;

        private readonly ConcurrentDictionary<ModuleIdentifier, ModuleProperties> _cache;

        public RemoteModulePropertiesLookup(IMessageDispatcher messageDispatcher, ILogger<RemoteModulePropertiesLookup> logger = null)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _messageDispatcher = messageDispatcher;
            _logger = logger;

            _cache = new ConcurrentDictionary<ModuleIdentifier, ModuleProperties>();
        }

        public ValueTask<ModuleProperties> LookupAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (_cache.TryGetValue(module, out var moduleProperties))
            {
                return new ValueTask<ModuleProperties>(moduleProperties);
            }

            return InternalLookupAsync(module, cancellation);
        }

        private async ValueTask<ModuleProperties> InternalLookupAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var maxTries = 10;
            var timeToWait = new TimeSpan(TimeSpan.TicksPerSecond * 1);
            var timeToWaitMax = new TimeSpan(TimeSpan.TicksPerSecond * 16);

            for (var i = 0; i < maxTries; i++)
            {
                var query = new ModulePropertiesQuery(module);
                var queryResult = await _messageDispatcher.DispatchAsync(query, cancellation);

                if (queryResult.IsSuccessWithResult<ModuleProperties>(out var moduleProperties))
                {
                    Assert(moduleProperties != null);
                    return _cache.GetOrAdd(module, moduleProperties);
                }

                if (!queryResult.IsNotFound())
                {
                    _logger?.LogError($"Unable to lookup end-point for module '{module.Name}' for reason: {(queryResult.IsSuccess ? "Wrong type returned" : queryResult.ToString())}.");
                    break;
                }

                await Task.Delay(timeToWait, cancellation);

                var timeToWaitDouble = new TimeSpan(timeToWait.Ticks * 2);

                if (timeToWaitDouble <= timeToWaitMax)
                {
                    timeToWait = timeToWaitDouble;
                }
            }

            return null;
        }
    }
}
