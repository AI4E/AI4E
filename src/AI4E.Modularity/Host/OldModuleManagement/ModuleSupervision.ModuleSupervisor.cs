using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AI4E.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Modularity
{
    public sealed partial class ModuleSupervision
    {
        private sealed class ModuleSupervisor : IModuleSupervisor
        {
         
            private TaskCompletionSource<object> _processTermination;

            public ModuleSupervisor(IModuleInstallation installation)
            {
                System.Diagnostics.Debug.Assert(installation != null);
                ModuleInstallation = installation;
            }

            public IModuleInstallation ModuleInstallation { get; }

            public async Task StartModuleAsync()
            {
                // Start the module process
                StartProcess();
            }

            public async Task StopModuleAsync()
            {
                // Await module process termination
                await _processTermination.Task;
            }

            private void StartProcess()
            {
                _processTermination = new TaskCompletionSource<object>();

                //var entryPath = Path.Combine(ModuleInstallation.InstallationDirectory, ModuleInstallation.ModuleMetadata.EntryAssemblyPath);
                //var process = Process.Start("dotnet", "\"" + new DirectoryInfo(entryPath).FullName + "\" " + _moduleHost.LocalEndPoint.Port,);

                var arguments = ModuleInstallation.ModuleMetadata.EntryAssemblyArguments;

                //arguments = arguments.Replace("%PORT%", _moduleHost.LocalEndPoint.Port.ToString());

                var processStartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    WorkingDirectory = ModuleInstallation.InstallationDirectory,
                    FileName = ModuleInstallation.ModuleMetadata.EntryAssemblyCommand,
                    Arguments = arguments
                };

                var process = Process.Start(processStartInfo);

                process.EnableRaisingEvents = true;
                process.Exited += (s, e) => _processTermination.TrySetResult(null);
            }
        }
    }
}
