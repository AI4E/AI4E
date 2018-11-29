using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Routing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Blazor.Modularity
{
    // TODO: Rename
    // TODO: Lookup end-point and prefix with a single query.
    internal sealed class RemoteModulePrefixLookup : IModulePrefixLookup
    {
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly ILogger<RemoteModulePrefixLookup> _logger;

        private readonly ConcurrentDictionary<ModuleIdentifier, string> _prefixCache;
        private readonly ConcurrentDictionary<ModuleIdentifier, EndPointAddress> _endPointCache;

        public RemoteModulePrefixLookup(IMessageDispatcher messageDispatcher, ILogger<RemoteModulePrefixLookup> logger = null)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _messageDispatcher = messageDispatcher;
            _logger = logger;
            _prefixCache = new ConcurrentDictionary<ModuleIdentifier, string>();
            _endPointCache = new ConcurrentDictionary<ModuleIdentifier, EndPointAddress>();
        }

        public ValueTask<string> LookupPrefixAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (_prefixCache.TryGetValue(module, out var modulePrefix))
            {
                return new ValueTask<string>(modulePrefix);
            }

            return new ValueTask<string>(InternalLookupPrefixAsync(module, cancellation));
        }

        public ValueTask<EndPointAddress> LookupEndPointAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (_endPointCache.TryGetValue(module, out var endPoint))
            {
                return new ValueTask<EndPointAddress>(endPoint);
            }

            return new ValueTask<EndPointAddress>(InternalLookupEndPointAsync(module, cancellation));
        }

        private async Task<EndPointAddress> InternalLookupEndPointAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var maxTries = 10;
            var timeToWait = new TimeSpan(TimeSpan.TicksPerSecond * 1);
            var timeToWaitMax = new TimeSpan(TimeSpan.TicksPerSecond * 16);

            for (var i = 0; i < maxTries; i++)
            {
                var query = new LookupModuleEndPoint(module);
                var queryResult = await _messageDispatcher.DispatchAsync(query, cancellation);

                if (queryResult.IsSuccessWithResult<EndPointAddress>(out var endPoint))
                {
                    Assert(endPoint != null);
                    _endPointCache.TryAdd(module, endPoint);
                    return endPoint;
                }

                if (!queryResult.IsNotFound())
                {
                    _logger?.LogError($"Unable to lookup prefix for module '{module.Name}' for reason: {(queryResult.IsSuccess ? "Wrong type returned" : queryResult.ToString())}.");
                    break;
                }

                await Task.Delay(timeToWait, cancellation);

                var timeToWaitDouble = new TimeSpan(timeToWait.Ticks * 2);

                if (timeToWaitDouble <= timeToWaitMax)
                {
                    timeToWait = timeToWaitDouble;
                }
            }

            throw new ModulePrefixLookupException();
        }

        private async Task<string> InternalLookupPrefixAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var maxTries = 10;
            var timeToWait = new TimeSpan(TimeSpan.TicksPerSecond * 1);
            var timeToWaitMax = new TimeSpan(TimeSpan.TicksPerSecond * 16);

            for (var i = 0; i < maxTries; i++)
            {
                var query = new LookupModulePrefix(module);
                var queryResult = await _messageDispatcher.DispatchAsync(query, cancellation);

                if (queryResult.IsSuccessWithResult<string>(out var modulePrefix))
                {
                    Assert(modulePrefix != null);
                    _prefixCache.TryAdd(module, modulePrefix);
                    return modulePrefix;
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

            throw new ModulePrefixLookupException();
        }
    }
}
