using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity
{
    internal sealed class SerializedModuleMetadata : IModuleMetadata
    {
        [JsonConstructor]
        private SerializedModuleMetadata() { }

        public SerializedModuleMetadata(IModuleMetadata metadata)
        {          
            Assert(metadata != null);

            if (metadata.Module == default || metadata.Version == default)
                throw new ArgumentException("Neither the metadata's module nor its version must be the respective type's default value.", nameof(metadata));

            Module = metadata.Module;
            Version = metadata.Version;
            ReleaseDate = metadata.ReleaseDate;
            Name = metadata.Name;
            Description = metadata.Description;
            Author = metadata.Author;
            EntryAssemblyCommand = metadata.EntryAssemblyCommand;
            EntryAssemblyArguments = metadata.EntryAssemblyArguments;

            foreach (var (module, versionRange) in metadata.Dependencies)
            {
                Dependencies.Add(module, versionRange);
            }
        }

        [JsonProperty("module")]
        public ModuleIdentifier Module { get; set; }

        [JsonProperty("version")]
        public ModuleVersion Version { get; set; }

        ModuleReleaseIdentifier IModuleMetadata.Release => new ModuleReleaseIdentifier(Module, Version);

        [JsonProperty("release-date")]
        public DateTime ReleaseDate { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("entry-command")]
        public string EntryAssemblyCommand { get; set; }

        [JsonProperty("entry-arguments")]
        public string EntryAssemblyArguments { get; set; }

        IEnumerable<ModuleDependency> IModuleMetadata.Dependencies => Dependencies.Select(p => new ModuleDependency(p.Key, p.Value));

        [JsonProperty("dependencies")]
        public Dictionary<ModuleIdentifier, ModuleVersionRange> Dependencies { get; } = new Dictionary<ModuleIdentifier, ModuleVersionRange>();
    }
}
