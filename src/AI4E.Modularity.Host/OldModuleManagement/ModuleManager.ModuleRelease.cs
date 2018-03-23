using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public sealed partial class ModuleManager
    {
        private sealed class ModuleRelease : IModuleRelease
        {
            private readonly IModuleInstaller _moduleInstaller;
            private readonly IModuleMetadata _metadata;

            public ModuleRelease(IModuleMetadata metadata, IModuleInstaller moduleInstaller, IModuleSource source)
            {
                Debug.Assert(metadata != null);
                Debug.Assert(moduleInstaller != null);
                Debug.Assert(source != null);

                _moduleInstaller = moduleInstaller;
                Source = source;
                _metadata = metadata;
            }

            public ModuleReleaseIdentifier Identifier => new ModuleReleaseIdentifier(_metadata.Id, Version);

            public bool IsPreRelease => Version.IsPreRelease;

            public ModuleVersion Version => _metadata.Version;

            public DateTime ReleaseDate => _metadata.ReleaseDate;

            public string DescriptiveName => _metadata.Name;

            public string Description => _metadata.Description;

            public ModuleIcon Icon => default;

            public string Author => _metadata.Author;

            public string ReferencePageUri => default;

            public bool IsInstalled => _moduleInstaller.IsInstalled(Identifier);

            public IModuleSource Source { get; }

            public Task InstallAsync()
            {
                if (IsInstalled)
                {
                    return Task.CompletedTask;
                }

                return _moduleInstaller.InstallAsync(Identifier, Source);
            }

            public Task UninstallAsync()
            {
                if (!IsInstalled)
                {
                    return Task.CompletedTask;
                }

                return _moduleInstaller.UninstallAsync(Identifier.Module);
            }
        }
    }
}
