using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Projection
{
    public interface IProjectionEngine
    {
        Task ProjectAsync(Type sourceType, string sourceId, CancellationToken cancellation = default);
    }
}