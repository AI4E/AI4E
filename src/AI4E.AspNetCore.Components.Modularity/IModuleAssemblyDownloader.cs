using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal interface IModuleAssemblyDownloader
    {
        Assembly GetAssembly(string assemblyName);

        ValueTask<Assembly> InstallAssemblyAsync(ModuleIdentifier module, string assemblyName, CancellationToken cancellation);
    }
}
