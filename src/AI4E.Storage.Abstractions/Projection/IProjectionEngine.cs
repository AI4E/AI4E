using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Projection
{
    public interface IProjectionEngine
    {
        Task ProjectAsync(Type entityType, string id, CancellationToken cancellation = default);
    }
}