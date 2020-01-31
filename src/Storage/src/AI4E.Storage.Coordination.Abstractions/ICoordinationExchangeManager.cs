using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Storage.Coordination
{
    public interface ICoordinationExchangeManager : IDisposable
    {
        Task NotifyReadLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation = default);
        Task NotifyWriteLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation = default);
        Task InvalidateCacheEntryAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation = default);
    }

    public interface ICoordinationExchangeManager<TAddress> : ICoordinationExchangeManager
    {
        ValueTask<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation = default);
    }
}
