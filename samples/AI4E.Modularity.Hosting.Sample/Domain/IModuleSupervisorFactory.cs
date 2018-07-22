using System.IO;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public interface IModuleSupervisorFactory
    {
        IModuleSupervisor CreateSupervisor(DirectoryInfo directory);
    }
}