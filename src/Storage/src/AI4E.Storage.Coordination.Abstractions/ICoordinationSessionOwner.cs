using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Coordination
{
    public interface ICoordinationSessionOwner : IDisposable
    {
        ValueTask<Session> GetSessionAsync(CancellationToken cancellation);
    }
}