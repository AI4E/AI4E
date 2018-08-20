using AI4E.Modularity.Host;
using AI4E.Modularity.Hosting.Sample.Models;
using AI4E.Storage.Projection;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleSourceProjection : Projection
    {
        public ModuleSourceModel Project(IModuleSource moduleSource)
        {
            if (moduleSource is FileSystemModuleSource fileSystemModuleSource)
            {
                return new ModuleSourceModel
                {
                    Id = fileSystemModuleSource.Id,
                    ConcurrencyToken = fileSystemModuleSource.ConcurrencyToken,
                    Location = fileSystemModuleSource.Location.Location,
                    Name = fileSystemModuleSource.Name.Value
                };
            }

            return null;
        }

        public ModuleSourceListModel ProjectToListModel(IModuleSource moduleSource)
        {
            if (moduleSource is FileSystemModuleSource fileSystemModuleSource)
            {
                return new ModuleSourceListModel
                {
                    Id = fileSystemModuleSource.Id,
                    Location = fileSystemModuleSource.Location.Location,
                    Name = fileSystemModuleSource.Name.Value
                };
            }

            return null;
        }

        public ModuleSourceDeleteModel ProjectToDeleteModel(IModuleSource moduleSource)
        {
            if (moduleSource is FileSystemModuleSource fileSystemModuleSource)
            {
                return new ModuleSourceDeleteModel
                {
                    Id = fileSystemModuleSource.Id,
                    ConcurrencyToken = fileSystemModuleSource.ConcurrencyToken,
                    Name = fileSystemModuleSource.Name.Value
                };
            }

            return null;
        }

        public ModuleSourceRenameModel ProjectToRenameModel(IModuleSource moduleSource)
        {
            if (moduleSource is FileSystemModuleSource fileSystemModuleSource)
            {
                return new ModuleSourceRenameModel
                {
                    Id = fileSystemModuleSource.Id,
                    ConcurrencyToken = fileSystemModuleSource.ConcurrencyToken,
                    Name = fileSystemModuleSource.Name.Value
                };
            }

            return null;
        }

        public ModuleSourceUpdateLocationModel ProjectToUpdateLocationModel(IModuleSource moduleSource)
        {
            if (moduleSource is FileSystemModuleSource fileSystemModuleSource)
            {
                return new ModuleSourceUpdateLocationModel
                {
                    Id = fileSystemModuleSource.Id,
                    ConcurrencyToken = fileSystemModuleSource.ConcurrencyToken,
                    Location = fileSystemModuleSource.Location.Location
                };
            }

            return null;
        }
    }
}
