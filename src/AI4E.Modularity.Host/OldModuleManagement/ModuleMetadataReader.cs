using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AI4E.Modularity
{
    internal static class ModuleMetadataReader
    {
        public static async Task<IModuleMetadata> ReadAsync(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            using (var manifestStream = stream)
            using (var manifestReader = new JsonTextReader(new StreamReader(memoryStream)))
            {
                await manifestStream.CopyToAsync(memoryStream, 4096);
                memoryStream.Position = 0;

                var moduleManifest = JsonSerializer.Create().Deserialize<ModuleMetadata>(manifestReader);

                // Invalid package
                if (moduleManifest == null)
                {
                    return null;
                }

                return moduleManifest;
            }
        }

        private sealed class ModuleMetadata : IModuleMetadata
        {
            private string _descriptiveName;

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("version")]
            public ModuleVersion Version { get; set; }

            [JsonProperty("release-date")]
            public DateTime ReleaseDate { get; set; }

            [JsonProperty("descriptive-name")]
            public string DescriptiveName
            {
                get => _descriptiveName ?? Name;
                set => _descriptiveName = value;
            }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonIgnore]
            public ModuleIcon Icon { get; set; }

            [JsonProperty("author")]
            public string Author { get; set; }

            [JsonProperty("reference-page")]
            public string ReferencePageUri { get; set; }

            [JsonProperty("entry-assembly-command")]
            public string EntryAssemblyCommand { get; set; }

            [JsonProperty("entry-assembly-arguments")]
            public string EntryAssemblyArguments { get; set; }

            ICollection<IModuleDependency> IModuleMetadata.Dependencies => Dependencies.ToArray();

            [JsonProperty("dependencies")]
            List<ModuleDependency> Dependencies { get; } = new List<ModuleDependency>();
        }

        private sealed class ModuleDependency : IModuleDependency
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("version")]
            public ModuleVersionFilter Version { get; set; }
        }
    }
}
