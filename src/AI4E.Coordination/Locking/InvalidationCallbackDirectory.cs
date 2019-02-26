using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination.Locking
{
    public sealed class InvalidationCallbackDirectory : IInvalidationCallbackDirectory
    {
        private readonly ConcurrentDictionary<string, Func<CancellationToken, ValueTask>> _callbacks = new ConcurrentDictionary<string, Func<CancellationToken, ValueTask>>();

        public InvalidationCallbackDirectory() { }

        public void Register(string key, Func<CancellationToken, ValueTask> callback)
        {
            _callbacks.AddOrUpdate(key, callback, (_, current) => current + callback);
        }

        public void Unregister(string key, Func<CancellationToken, ValueTask> callback)
        {
            while (_callbacks.TryGetValue(key, out var current) && !_callbacks.TryUpdate(key, current + callback, current)) { }
        }

        public ValueTask InvokeAsync(string key, CancellationToken cancellation)
        {
            if (!_callbacks.TryGetValue(key, out var callback))
            {
                return default;
            }

            var invocationList = (Func<CancellationToken, ValueTask>[])callback.GetInvocationList();
            return new ValueTask(Task.WhenAll(invocationList.Select(p => p(cancellation).AsTask()))); // TODO: Use ValueTaskHelper
        }
    }
}
