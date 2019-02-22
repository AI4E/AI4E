using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    internal interface IModuleAssemblyDownloader
    {
        Assembly GetAssembly(string assemblyName);

        ValueTask<Assembly> InstallAssemblyAsync(ModuleIdentifier module, string assemblyName, CancellationToken cancellation);
        Task<byte[]> DownloadAssemblyAsync(ModuleIdentifier module, string assemblyName, CancellationToken cancellation);
    }
}
