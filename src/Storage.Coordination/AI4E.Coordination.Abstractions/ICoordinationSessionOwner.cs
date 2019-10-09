using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination
{
    public interface ICoordinationSessionOwner : IDisposable
    {
        ValueTask<Session> GetSessionAsync(CancellationToken cancellation);
    }
}