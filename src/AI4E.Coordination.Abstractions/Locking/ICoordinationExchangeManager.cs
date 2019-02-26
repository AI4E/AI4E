using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Remoting;

namespace AI4E.Coordination.Locking
{
    public interface ICoordinationExchangeManager : IDisposable
    {
        ValueTask NotifyReadLockReleasedAsync(
            string key,
            CancellationToken cancellation = default);

        ValueTask NotifyWriteLockReleasedAsync(
            string key,
            CancellationToken cancellation = default);

        ValueTask InvalidateCacheEntryAsync(
            string key,
            CoordinationSession session,
            CancellationToken cancellation = default);
    }

    public interface ICoordinationExchangeManager<TAddress> : ICoordinationExchangeManager
    {
        ValueTask<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(
            CancellationToken cancellation = default);
    }
}
