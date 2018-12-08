using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Storage.Domain;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleInstallationManager : IModuleInstallationManager
    {
        private readonly IMetadataReader _metadataReader;
        private readonly IModuleInstaller _moduleInstaller;
        private readonly IModuleSupervisorFactory _moduleSupervisorFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly HashSet<IModuleSupervisor> _supervisors = new HashSet<IModuleSupervisor>();
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly DirectoryInfo _installationDirectory;
        private readonly AsyncInitializationHelper _initializationHelper;

        public ModuleInstallationManager(IMetadataReader metadataReader,
                             IModuleInstaller moduleInstaller,
                             IModuleSupervisorFactory moduleSupervisorFactory,
                             IServiceProvider serviceProvider,
                             IOptions<ModuleManagementOptions> optionsAccessor)
        {
            if (metadataReader == null)
                throw new ArgumentNullException(nameof(metadataReader));

            if (moduleInstaller == null)
                throw new ArgumentNullException(nameof(moduleInstaller));

            if (moduleSupervisorFactory == null)
                throw new ArgumentNullException(nameof(moduleSupervisorFactory));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _metadataReader = metadataReader;
            _moduleInstaller = moduleInstaller;
            _moduleSupervisorFactory = moduleSupervisorFactory;
            _serviceProvider = serviceProvider;
            var options = optionsAccessor.Value ?? new ModuleManagementOptions();

            _installationDirectory = new DirectoryInfo(options.ModuleInstallationDirectory);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
        }

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            var installationSet = await GetInstallationSetAsync(cancellation);

            if (_installationDirectory.Exists)
            {
                try
                {
                    foreach (var moduleInstallationDirectory in _installationDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        _supervisors.Add(_moduleSupervisorFactory.CreateSupervisor(moduleInstallationDirectory));
                    }
                }
                catch (DirectoryNotFoundException) { }
            }

            await ConfigureInstallationSetCoreAsync(installationSet, cancellation);
        }

        // TODO: This should be abstracted in order to support clustering.
        private async Task<ResolvedInstallationSet> GetInstallationSetAsync(CancellationToken cancellation)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>();
                var installationConfiguration = await storageEngine.GetByIdAsync<ModuleInstallationConfiguration>(default(SingletonId).ToString(), cancellation);

                if (installationConfiguration == null)
                {
                    return ResolvedInstallationSet.EmptyInstallationSet;
                }

                return installationConfiguration.ResolvedModules;
            }
        }

        public async Task ConfigureInstallationSetAsync(ResolvedInstallationSet installationSet, CancellationToken cancellation)
        {
            await _initializationHelper.Initialization.WithCancellation(cancellation);

            using (await _lock.LockAsync(cancellation))
            {
                await ConfigureInstallationSetCoreAsync(installationSet, cancellation);
            }
        }

        private async Task ConfigureInstallationSetCoreAsync(ResolvedInstallationSet installationSet, CancellationToken cancellation)
        {
            var resolvedReleases = installationSet.Resolved.ToHashSet();
            var runningReleases = await GetSupervisorsAsync(cancellation);
            var union = resolvedReleases.Intersect(runningReleases.Keys).ToArray();

            resolvedReleases.ExceptWith(union);

            foreach (var entry in union)
            {
                runningReleases.Remove(entry);
            }

            await Task.WhenAll(runningReleases.Select(p => UninstallAsync(p.Value, cancellation)));

            await Task.WhenAll(resolvedReleases.Select(p => InstallAsync(p, cancellation)));
        }

        private async Task InstallAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            var moduleInstallationDirectory = await _moduleInstaller.InstallAsync(_installationDirectory, moduleRelease, cancellation);

            if (moduleInstallationDirectory == null)
            {
                // TODO
                throw new Exception();
            }

            var supervisor = _moduleSupervisorFactory.CreateSupervisor(moduleInstallationDirectory);
            _supervisors.Add(supervisor);
        }

        private async Task UninstallAsync(IModuleSupervisor supervisor, CancellationToken cancellation)
        {
            Assert(supervisor != null);

            var moduleInstallationDirectory = supervisor.Directory;

            await supervisor.DisposeAsync();
            _supervisors.Remove(supervisor);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    moduleInstallationDirectory.Delete(recursive: true);
                }
                catch (DirectoryNotFoundException) { }
                catch
                {
                    // TODO: Log warning

                    await Task.Delay(1000);
                }
            }
        }

        private async Task<IDictionary<ModuleReleaseIdentifier, IModuleSupervisor>> GetSupervisorsAsync(CancellationToken cancellation)
        {
            async Task<(ModuleReleaseIdentifier release, IModuleSupervisor supervisor)> ResolveReleaseAsync(IModuleSupervisor supervisor)
            {
                var release = await supervisor.GetSupervisedModule(cancellation);

                return (release, supervisor);
            }

            var taggedSupervisors = await Task.WhenAll(_supervisors.Select(supervisor => ResolveReleaseAsync(supervisor)));

            return taggedSupervisors.ToDictionary(p => p.release, p => p.supervisor);
        }
    }

    public sealed class ModuleManagementOptions
    {
        public string ModuleInstallationDirectory { get; set; } = Path.Combine(".", "modules");
    }
}
