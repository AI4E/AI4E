using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination.Session
{
    public interface ICoordinationSessionOwner : IDisposable
    {
        ValueTask<CoordinationSession> GetSessionAsync(CancellationToken cancellation);
    }
}
