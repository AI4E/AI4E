using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    public interface IModuleInstaller
    {
        Task<DirectoryInfo> InstallAsync(DirectoryInfo directory, ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation);
    }
}