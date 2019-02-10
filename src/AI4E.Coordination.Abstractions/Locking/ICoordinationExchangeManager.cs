using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Remoting;

namespace AI4E.Coordination.Locking
{
    public interface ICoordinationExchangeManager : IDisposable
    {
        Task NotifyReadLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation = default);
        Task NotifyWriteLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation = default);
        Task InvalidateCacheEntryAsync(CoordinationEntryPath path, CoordinationSession session, CancellationToken cancellation = default);
    }

    public interface ICoordinationExchangeManager<TAddress> : ICoordinationExchangeManager
    {
        ValueTask<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation = default);
    }
}
