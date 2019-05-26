using System.IO;

namespace AI4E.Modularity.Host
{
    public interface IModuleSupervisorFactory
    {
        IModuleSupervisor CreateSupervisor(DirectoryInfo directory);
    }
}