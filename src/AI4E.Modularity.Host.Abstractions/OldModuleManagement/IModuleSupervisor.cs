using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public interface IModuleSupervisor
    {
        IModuleInstallation ModuleInstallation { get; }

        Task StartModuleAsync();

        Task StopModuleAsync();
    }
}
