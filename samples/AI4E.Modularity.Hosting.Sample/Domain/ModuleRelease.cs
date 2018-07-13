using System;
using System.Collections.Generic;
using System.Linq;
using AI4E.Domain;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleRelease
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

        // The private setter is necessary due to a bug in JSON.Net https://github.com/JamesNK/Newtonsoft.Json/issues/1284
        public Module Module { get; private set; }

        [JsonIgnore]
        public bool IsInstalled => Module.InstalledRelease == this;

        [JsonIgnore]
        public ModuleVersion Version => _metadata.Version;

        [JsonIgnore]
        public IEnumerable<Snapshot<FileSystemModuleSource>> Sources => _sources; // TODO: Create read-only wrapper

        [JsonIgnore]
        DateTime ReleaseDate => _metadata.ReleaseDate;

        [JsonIgnore]
        string Name => _metadata.Name;

        [JsonIgnore]
        string Description => _metadata.Description;

        [JsonIgnore]
        string Author => _metadata.Author;

        public void AddSource(FileSystemModuleSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            _sources.Add(source);
        }

        public void RemoveSource(FileSystemModuleSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            _sources.Remove(source);

            // If there are no more sources available for the release, we have to remove it.
            if (!_sources.Any())
            {
                Module.RemoveModuleRelease(this);
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
