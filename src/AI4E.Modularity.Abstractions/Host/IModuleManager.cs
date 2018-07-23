using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    public interface IModuleManager
    {
        Task ConfigureInstallationSetAsync(ResolvedInstallationSet installationSet, CancellationToken cancellation);
    }
}
