using System.Threading.Tasks;
using AI4E.Storage.Domain;

namespace AI4E.Modularity.Host
{
    // TODO: Are events arising from a single aggregate guaranteed to arrive in order? 
    //       If this is not the case, we have to reorder them.
    public sealed class ModuleInstallationProcessManager : MessageHandler<ModuleInstallationConfiguration>
    {
        private readonly IDependencyResolver _dependencyResolver;
        private readonly IEntityStorageEngine _storageEngine;

        public ModuleInstallationProcessManager(IDependencyResolver dependencyResolver,
                                                IEntityStorageEngine storageEngine)
        {
            if (dependencyResolver == null)
                throw new System.ArgumentNullException(nameof(dependencyResolver));

            if (storageEngine == null)
                throw new System.ArgumentNullException(nameof(storageEngine));

            _dependencyResolver = dependencyResolver;
            _storageEngine = storageEngine;
        }

        [EntityLookup]
        private Task<ModuleInstallationConfiguration> LookupConfigurationAsync()
        {
            return _storageEngine.GetByIdAsync<ModuleInstallationConfiguration>(default(SingletonId).ToString()).AsTask();
        }

        #region InstallationConfigurationChanged

        [CreatesEntity(AllowExisingEntity = true)]
        public Task HandleAsync(ModuleInstalled installedEvent)
        {
            return EnsureEntity().ModuleInstalledAsync(installedEvent.ModuleId, installedEvent.Version, _dependencyResolver);
        }

        [CreatesEntity(AllowExisingEntity = true)]
        public Task HandleAsync(ModuleUpdated updatedEvent)
        {
            return EnsureEntity().ModuleUpdatedAsync(updatedEvent.ModuleId, updatedEvent.UpdatedVersion, _dependencyResolver);
        }

        [CreatesEntity(AllowExisingEntity = true)]
        public Task HandleAsync(ModuleUninstalled uninstalledEvent)
        {
            return EnsureEntity().ModuleUninstalledAsync(uninstalledEvent.ModuleId, _dependencyResolver);
        }

        #endregion

        // We do actually want to ignore the event if the manager does not exist. 
        // The message processor does not have an attribute to setup this functionality currently.
        [CreatesEntity(AllowExisingEntity = true)]
        public Task HandleAsync(ModuleReleaseAdded releaseAddedEvent)
        {
            if (Entity == null)
            {
                return Task.CompletedTask;
            }

            return Entity.ReleaseAddedAsync(releaseAddedEvent.ModuleId, releaseAddedEvent.Version, _dependencyResolver);
        }

        // We do actually want to ignore the event if the manager does not exist. 
        // The message processor does not have an attribute to setup this functionality currently.
        [CreatesEntity(AllowExisingEntity = true)]
        public Task HandleAsync(ModuleReleaseRemoved releaseRemovedEvent)
        {
            if (Entity == null)
            {
                return Task.CompletedTask;
            }

            return Entity.ReleaseAddedAsync(releaseRemovedEvent.ModuleId, releaseRemovedEvent.Version, _dependencyResolver);
        }

        private ModuleInstallationConfiguration EnsureEntity()
        {
            if (Entity == null)
                Entity = new ModuleInstallationConfiguration();

            return Entity;
        }
    }
}
