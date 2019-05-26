using System;

namespace AI4E.Modularity.Host
{
    public sealed class FileSystemModuleSourceLocationChanged
    {
        public FileSystemModuleSourceLocationChanged(Guid moduleSourceId, FileSystemModuleSourceLocation location)
        {
            ModuleSourceId = moduleSourceId;
            Location = location;
        }

        public Guid ModuleSourceId { get; }
        public FileSystemModuleSourceLocation Location { get; }
    }
}
