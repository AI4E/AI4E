using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Projection
{
    public interface IProjectionSourceLoader
    {
        ValueTask<(object projectionSource, long revision)> LoadAsync(Type projectionSourceType, string projectionSourceId, CancellationToken cancellation);

        IEnumerable<(Type type, string id, long revision)> LoadedSources { get; }
    }
}
