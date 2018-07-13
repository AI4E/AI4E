using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public interface IModuleSearchEngine
    {
        Task<IEnumerable<Module>> SearchModulesAsync(string searchPhrase, 
                                                     bool includePreReleases, 
                                                     CancellationToken cancellation);
    }
}