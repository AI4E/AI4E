using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination.Locking
{
    public interface IInvalidationCallbackDirectory
    {
        void Register(string key, Func<CancellationToken, ValueTask> callback);
        void Unregister(string key, Func<CancellationToken, ValueTask> callback);

        ValueTask InvokeAsync(string key, CancellationToken cancellation);
    }
}
