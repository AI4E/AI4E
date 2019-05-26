using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    public interface IModuleSearchEngine
    {
        Task<IEnumerable<IModule>> SearchModulesAsync(string searchPhrase, 
                                                     bool includePreReleases, 
                                                     CancellationToken cancellation);
    }
}