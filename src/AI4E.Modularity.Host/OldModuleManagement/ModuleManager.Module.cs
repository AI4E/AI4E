using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public sealed partial class ModuleManager
    {
        private sealed class Module : IModule
        {
            private readonly IModuleInstaller _moduleInstaller;
            private readonly ImmutableArray<ModuleRelease> _releases;

            public Module(ModuleIdentifier identifier, IEnumerable<ModuleRelease> releases, IModuleInstaller moduleInstaller)
            {
                Debug.Assert(identifier != ModuleIdentifier.UnknownModule);
                Debug.Assert(releases != null);
                Debug.Assert(releases.Any());
                Debug.Assert(moduleInstaller != null);

                Identifier = identifier;
                _releases = releases.ToImmutableArray();
                _moduleInstaller = moduleInstaller;

                IsDebugModule = false;
            }

            public Module(ModuleIdentifier identifier, IModuleInstaller moduleInstaller)
            {
                Debug.Assert(identifier != ModuleIdentifier.UnknownModule);
                Debug.Assert(moduleInstaller != null);

                Identifier = identifier;
                _releases = ImmutableArray<ModuleRelease>.Empty;
                _moduleInstaller = moduleInstaller;

                IsDebugModule = true;
            }

            public ModuleIdentifier Identifier { get; }

            public DateTime? ReleaseDate => LatestRelease?.ReleaseDate;

            public string DescriptiveName => LatestRelease?.DescriptiveName ?? Identifier.Name;

            public string Description => LatestRelease?.Description;

            public ModuleIcon Icon => LatestRelease?.Icon ?? default;

            public string Author => LatestRelease?.Author;

            public string ReferencePageUri => LatestRelease?.ReferencePageUri;

            public bool IsDebugModule { get; }

            public IEnumerable<IModuleRelease> Releases => _releases;

            // TODO: The latest release is the one with the greatest version number.
            public IModuleRelease LatestRelease => Releases.Aggregate(default(IModuleRelease), (latest, current) => latest == default(IModuleRelease) || latest.ReleaseDate < current.ReleaseDate ? current : latest);

            public IModuleRelease InstalledRelease => Releases.SingleOrDefault(p => p.IsInstalled);

            public bool IsInstalled => Releases.Any(p => p.IsInstalled);

            public bool IsLatestReleaseInstalled => LatestRelease?.IsInstalled ?? false;

            public Task InstallAsync()
            {
                if (IsDebugModule)
                {
                    return Task.CompletedTask;
                }

                return LatestRelease.InstallAsync();
            }

            public Task UninstallAsync()
            {
                if (IsDebugModule || !IsInstalled)
                {
                    return Task.CompletedTask;
                }

                return _moduleInstaller.UninstallAsync(Identifier);
            }
        }
    }
}
