using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public interface IModuleManager
    {
        Task ConfigureInstallationSetAsync(ResolvedInstallationSet installationSet, CancellationToken cancellation);
    }
}
