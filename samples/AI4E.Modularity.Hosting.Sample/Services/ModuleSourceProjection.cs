using AI4E.Modularity.Host;
using AI4E.Modularity.Hosting.Sample.Models;
using AI4E.Storage.Projection;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleSourceProjection : Projection
    {
        public ModuleSourceModel Project(FileSystemModuleSource moduleSource)
        {
            // TODO: This should be the default with a way to override it (via an attribute??)
            if (moduleSource == null)
                return null;

            return new ModuleSourceModel
            {
                Id = moduleSource.Id,
                ConcurrencyToken = moduleSource.ConcurrencyToken,
                Location = moduleSource.Location.Location,
                Name = moduleSource.Name.Value
            };
        }

        public ModuleSourceListModel ProjectToListModel(FileSystemModuleSource moduleSource)
        {
            // TODO: This should be the default with a way to override it (via an attribute??)
            if (moduleSource == null)
                return null;

            return new ModuleSourceListModel
            {
                Id = moduleSource.Id,
                Location = moduleSource.Location.Location,
                Name = moduleSource.Name.Value
            };
        }

        public ModuleSourceDeleteModel ProjectToDeleteModel(FileSystemModuleSource moduleSource)
        {
            // TODO: This should be the default with a way to override it (via an attribute??)
            if (moduleSource == null)
                return null;

            return new ModuleSourceDeleteModel
            {
                Id = moduleSource.Id,
                ConcurrencyToken = moduleSource.ConcurrencyToken,
                Name = moduleSource.Name.Value
            };
        }

        public ModuleSourceRenameModel ProjectToRenameModel(FileSystemModuleSource moduleSource)
        {
            // TODO: This should be the default with a way to override it (via an attribute??)
            if (moduleSource == null)
                return null;

            return new ModuleSourceRenameModel
            {
                Id = moduleSource.Id,
                ConcurrencyToken = moduleSource.ConcurrencyToken,
                Name = moduleSource.Name.Value
            };
        }

        public ModuleSourceUpdateLocationModel ProjectToUpdateLocationModel(FileSystemModuleSource moduleSource)
        {
            // TODO: This should be the default with a way to override it (via an attribute??)
            if (moduleSource == null)
                return null;

            return new ModuleSourceUpdateLocationModel
            {
                Id = moduleSource.Id,
                ConcurrencyToken = moduleSource.ConcurrencyToken,
                Location = moduleSource.Location.Location
            };
        }
    }
}
