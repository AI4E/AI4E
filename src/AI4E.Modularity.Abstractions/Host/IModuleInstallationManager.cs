using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    public interface IModuleInstallationManager
    {
        Task ConfigureInstallationSetAsync(ResolvedInstallationSet installationSet, CancellationToken cancellation);
    }
}
