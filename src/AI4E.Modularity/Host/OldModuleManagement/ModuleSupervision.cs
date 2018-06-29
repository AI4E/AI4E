using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public sealed partial class ModuleSupervision : IModuleSupervision
    {
        private readonly ConcurrentDictionary<IModuleInstallation, ModuleSupervisor> _supervisors = new ConcurrentDictionary<IModuleInstallation, ModuleSupervisor>();

        public ModuleSupervision()
        {
        }

        public IModuleSupervisor GetSupervisor(IModuleInstallation installation)
        {
            if (_supervisors.TryGetValue(installation, out var result))
            {
                return result;
            }

            return default;
        }

        public async Task RegisterModuleInstallationAsync(IModuleInstallation installation)
        {
            if (_supervisors.TryAdd(installation, new ModuleSupervisor(installation)))
            {
                
            }
        }

        public async Task UnregisterModuleInstallationAsync(IModuleInstallation installation)
        {
            if (_supervisors.TryRemove(installation, out _))
            {
               
            }
        }
    }
}
