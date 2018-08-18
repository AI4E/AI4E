using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Host;

namespace AI4E.Blazor.Modularity
{
    public interface IInstallationSetManager
    {
        Task UpdateInstallationSetAsync(ResolvedInstallationSet installationSet, CancellationToken cancellation);
    }
}
