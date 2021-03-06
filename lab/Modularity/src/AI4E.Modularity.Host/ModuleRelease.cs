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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Modularity.Metadata;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleRelease : IModuleRelease
    {
        // We are storing snapshots to sources here because a source can change its location rendering it unusable for querying module information.
        [JsonProperty("Sources")]
        private readonly HashSet<Snapshot<IModuleSource>> _sources;

        [JsonProperty("Metadata")]
        private readonly SerializedModuleMetadata _metadata;

        internal ModuleRelease(Module module, IModuleMetadata metadata, IModuleSource moduleSource)
        {
            Assert(module != null);
            Assert(metadata != null);
            Assert(moduleSource != null);

            _sources = new HashSet<Snapshot<IModuleSource>> { new Snapshot<IModuleSource>(moduleSource) };
            _metadata = new SerializedModuleMetadata(metadata);
            Module = module;
        }

        [JsonConstructor]
        private ModuleRelease(SerializedModuleMetadata metadata)
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

        [JsonIgnore]
        public DateTime ReleaseDate => _metadata.ReleaseDate;

        [JsonIgnore]
        public string Name => !string.IsNullOrWhiteSpace(_metadata.Name) ? _metadata.Name : Module.Id.Name;

        [JsonIgnore]
        public string Description => _metadata.Description;

        [JsonIgnore]
        public string Author => _metadata.Author;

        [JsonIgnore]
        public IEnumerable<ModuleDependency> Dependencies => (_metadata as IModuleMetadata).Dependencies;

        public async ValueTask<IEnumerable<IModuleSource>> GetSourcesAsync(CancellationToken cancellation)
        {
            var sources = await Task.WhenAll(_sources.Select(p => p.ResolveAsync().AsTask())); // TODO: Cancellation, Implement a helper that works like Task.WhenAll but for ValueTasks

            return sources.Where(p => p != null);
        }

        public bool TryAddSource(IModuleSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return _sources.Add(new Snapshot<IModuleSource>(source));
        }

        public bool TryRemoveSource(IModuleSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = _sources.Remove(new Snapshot<IModuleSource>(source));

            // If there are no more sources available for the release, we have to remove it.
            if (!_sources.Any())
            {
                Module.RemoveRelease(this);
            }

            return result;
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

        private sealed class SerializedModuleMetadata : IModuleMetadata
        {
            [JsonConstructor]
            private SerializedModuleMetadata() { }

            public SerializedModuleMetadata(IModuleMetadata moduleMetadata)
            {
                if (moduleMetadata == null)
                    throw new ArgumentNullException(nameof(moduleMetadata));

                foreach (var item in moduleMetadata.Dependencies)
                {
                    Dependencies.Add(item.Module, item.VersionRange);
                }

                Author = moduleMetadata.Author;
                Description = moduleMetadata.Description;
                EntryAssemblyArguments = moduleMetadata.EntryAssemblyArguments;
                EntryAssemblyCommand = moduleMetadata.EntryAssemblyCommand;
                Module = moduleMetadata.Module;
                Name = moduleMetadata.Name;
                ReleaseDate = moduleMetadata.ReleaseDate;
                Version = moduleMetadata.Version;
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
            public Dictionary<ModuleIdentifier, ModuleVersionRange> Dependencies { get; set; } = new Dictionary<ModuleIdentifier, ModuleVersionRange>();
        }
    }
}
