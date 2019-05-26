using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Metadata;

namespace AI4E.Modularity.Host
{
    public interface IDependencyResolver
    {
        ValueTask<IEnumerable<ModuleReleaseIdentifier>> GetMatchingReleasesAsync(ModuleDependency dependency, CancellationToken cancellation);
        ValueTask<IEnumerable<ModuleDependency>> GetDependenciesAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation);
    }
}
