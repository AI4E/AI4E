/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using AI4E.Modularity.Metadata;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Metadata
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
                if (Dependencies == null)
                    Dependencies = new Dictionary<ModuleIdentifier, ModuleVersionRange>();

                Dependencies.Add(module, versionRange);
            }
        }

        [JsonProperty("module")]
        public ModuleIdentifier Module { get; set; }

        [JsonProperty("version")]
        public ModuleVersion Version { get; set; }

        ModuleReleaseIdentifier IModuleMetadata.Release => new ModuleReleaseIdentifier(Module, Version);

        [JsonProperty("release-date", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime ReleaseDate { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("author", NullValueHandling = NullValueHandling.Ignore)]
        public string Author { get; set; }

        [JsonProperty("entry-command", NullValueHandling = NullValueHandling.Ignore)]
        public string EntryAssemblyCommand { get; set; }

        [JsonProperty("entry-arguments", NullValueHandling = NullValueHandling.Ignore)]
        public string EntryAssemblyArguments { get; set; }

        IEnumerable<ModuleDependency> IModuleMetadata.Dependencies => Dependencies?.Select(p => new ModuleDependency(p.Key, p.Value)) ?? Enumerable.Empty<ModuleDependency>();

        [JsonProperty("dependencies", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<ModuleIdentifier, ModuleVersionRange> Dependencies { get; private set; }

        [OnDeserializing]
        private void OnDeserializingMethod(StreamingContext context)
        {
            if (Dependencies == null)
                Dependencies = new Dictionary<ModuleIdentifier, ModuleVersionRange>();
        }

        [OnDeserialized]
        private void OnDeserializedMethod(StreamingContext context)
        {
            if (!Dependencies.Any())
                Dependencies = null;
        }
    }
}
