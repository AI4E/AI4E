using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public interface IModuleSupervision
    {
        IModuleSupervisor GetSupervisor(IModuleInstallation installation);

        Task RegisterModuleInstallationAsync(IModuleInstallation installation);

        Task UnregisterModuleInstallationAsync(IModuleInstallation installation);
    }
}
