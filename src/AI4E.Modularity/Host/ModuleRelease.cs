using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleRelease : IModuleRelease
    {
        // We are storing snapshots to sources here because a source can change its location rendering it unusable for querying module information.
        [JsonProperty("Sources")]
        private readonly HashSet<Snapshot<FileSystemModuleSource>> _sources;

        [JsonProperty("Metadata")]
        private readonly IModuleMetadata _metadata;

        internal ModuleRelease(Module module, IModuleMetadata metadata, FileSystemModuleSource moduleSource)
        {
            Assert(module != null);
            Assert(metadata != null);
            Assert(moduleSource != null);

            _sources = new HashSet<Snapshot<FileSystemModuleSource>> { moduleSource };
            _metadata = metadata;
            Module = module;
        }

        [JsonConstructor]
        private ModuleRelease(IModuleMetadata metadata)
        {
            _metadata = metadata;
        }

        // This property is handled manually due to a bug in JSON.Net https://github.com/JamesNK/Newtonsoft.Json/issues/1284
        [JsonIgnore]
        public Module Module { get; internal set; }

        IModule IModuleRelease.Module => Module;

        [JsonIgnore]
        public ModuleReleaseIdentifier Id => new ModuleReleaseIdentifier(Module.Id, Version);

        [JsonIgnore]
        public bool IsInstalled => Module.InstalledRelease == this;

        [JsonIgnore]
        public ModuleVersion Version => _metadata.Version;

        [JsonIgnore, Obsolete("Use GetSourcesAsync(CancellationToken)")]
        public IEnumerable<Snapshot<FileSystemModuleSource>> Sources => _sources; // TODO: Create read-only wrapper

        [JsonIgnore]
        public DateTime ReleaseDate => _metadata.ReleaseDate;

        [JsonIgnore]
        public string Name => _metadata.Name;

        [JsonIgnore]
        public string Description => _metadata.Description;

        [JsonIgnore]
        public string Author => _metadata.Author;

        [JsonIgnore]
        public IEnumerable<ModuleDependency> Dependencies => _metadata.Dependencies;

        public async ValueTask<IEnumerable<IModuleSource>> GetSourcesAsync(CancellationToken cancellation)
        {
            var sources = await Task.WhenAll(_sources.Select(p => p.ResolveAsync().AsTask())); // TODO: Cancellation, Implement a helper that works like Task.WhenAll but for ValueTasks

            return sources.Where(p => p != null);
        }

        public void AddSource(IModuleSource source)
        {
            AddSource(source as FileSystemModuleSource); // TODO
        }

        public void AddSource(FileSystemModuleSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            _sources.Add(source);
        }

        public void RemoveSource(IModuleSource source)
        {
            RemoveSource(source as FileSystemModuleSource); // TODO
        }

        public void RemoveSource(FileSystemModuleSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            _sources.Remove(source);

            // If there are no more sources available for the release, we have to remove it.
            if (!_sources.Any())
            {
                Module.RemoveRelease(this);
            }
        }

        public void Install()
        {
            Module.Install(this);
        }

        public void Uninstall()
        {
            if (!IsInstalled)
                return;

            Module.Uninstall();
        }
    }
}
