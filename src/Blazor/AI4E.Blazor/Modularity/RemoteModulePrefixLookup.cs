using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using static System.Diagnostics.Debug;

namespace AI4E.Blazor.Modularity
{
    public sealed class RemoteModulePrefixLookup : IModulePrefixLookup
    {
        private readonly ConcurrentDictionary<ModuleIdentifier, string> _cache;
        private readonly IMessageDispatcher _messageDispatcher;

        public RemoteModulePrefixLookup(IMessageDispatcher messageDispatcher)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _messageDispatcher = messageDispatcher;
            _cache = new ConcurrentDictionary<ModuleIdentifier, string>();
        }

        public ValueTask<string> LookupPrefixAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (_cache.TryGetValue(module, out var modulePrefix))
            {
                return new ValueTask<string>(modulePrefix);
            }

            return new ValueTask<string>(InternalLookupPrefixAsync(module, modulePrefix, cancellation));
        }

        private async Task<string> InternalLookupPrefixAsync(ModuleIdentifier module, string modulePrefix, CancellationToken cancellation)
        {
            var maxTries = 10;
            var timeToWait = new TimeSpan(TimeSpan.TicksPerSecond * 1);
            var timeToWaitMax = new TimeSpan(TimeSpan.TicksPerSecond * 16);

            for (var i = 0; i < maxTries; i++)
            {
                var query = new LookupModulePrefix(module);
                var queryResult = await _messageDispatcher.DispatchAsync(query, cancellation);

                if (queryResult.IsSuccessWithResult(out modulePrefix))
                {
                    Assert(modulePrefix != null);
                    _cache.TryAdd(module, modulePrefix);
                    return modulePrefix;
                }

                if (!queryResult.IsNotFound())
                {
                    // TODO: Log failure
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
