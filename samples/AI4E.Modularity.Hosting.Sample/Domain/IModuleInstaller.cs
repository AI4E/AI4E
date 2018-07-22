using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public interface IModuleInstaller
    {
        Task<DirectoryInfo> InstallAsync(DirectoryInfo directory, ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation);
    }
}