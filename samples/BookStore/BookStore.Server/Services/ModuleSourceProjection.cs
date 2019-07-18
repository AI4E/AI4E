using AI4E.Modularity.Host;
using AI4E.Storage.Projection;
using BookStore.Models;

namespace BookStore.Server.Services
{
    public sealed class ModuleSourceProjection : Projection
    {
        public ModuleSourceModel Project(IModuleSource x/*FileSystemModuleSource moduleSource*/)
        {
            var moduleSource = x as FileSystemModuleSource;
            return new ModuleSourceModel
            {
                Id = moduleSource.Id,
                ConcurrencyToken = moduleSource.ConcurrencyToken,
                Location = moduleSource.Location.Location,
                Name = moduleSource.Name.Value
            };
        }

        public ModuleSourceListModel ProjectToListModel(IModuleSource x/*FileSystemModuleSource moduleSource*/)
        {
            var moduleSource = x as FileSystemModuleSource;
            return new ModuleSourceListModel
            {
                Id = moduleSource.Id,
                Location = moduleSource.Location.Location,
                Name = moduleSource.Name.Value
            };
        }

        public ModuleSourceDeleteModel ProjectToDeleteModel(IModuleSource moduleSource)
        {
            return new ModuleSourceDeleteModel
            {
                Id = moduleSource.Id,
                ConcurrencyToken = moduleSource.ConcurrencyToken,
                Name = moduleSource.Name.Value
            };
        }
    }
}
