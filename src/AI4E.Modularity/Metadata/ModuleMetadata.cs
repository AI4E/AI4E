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

namespace AI4E.Modularity.Metadata
{
    public sealed class ModuleMetadata : IModuleMetadata
    {
        private ModuleIdentifier _module;
        private ModuleVersion _version;
        private ModuleDependencyCollection _dependencies;

        public ModuleMetadata(ModuleIdentifier module, ModuleVersion version)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (version == default)
                throw new ArgumentDefaultException(nameof(version));

            _version = version;
            _module = module;
        }

        public ModuleMetadata(IModuleMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

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

            if(metadata.Dependencies.Any())
            {
                _dependencies = new ModuleDependencyCollection(metadata.Dependencies);
            }
        }

        public ModuleIdentifier Module
        {
            get => _module;
            set
            {
                if (value == default)
                    throw new ArgumentDefaultException(nameof(value));

                _module = value;
            }
        }

        public ModuleVersion Version
        {
            get => _version;
            set
            {
                if (value == default)
                    throw new ArgumentDefaultException(nameof(value));

                _version = value;
            }
        }

        ModuleReleaseIdentifier IModuleMetadata.Release => new ModuleReleaseIdentifier(Module, Version);

        public DateTime ReleaseDate { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Author { get; set; }

        public string EntryAssemblyCommand { get; set; }

        public string EntryAssemblyArguments { get; set; }

        IEnumerable<ModuleDependency> IModuleMetadata.Dependencies => _dependencies ?? Enumerable.Empty<ModuleDependency>();

        public ModuleDependencyCollection Dependencies => _dependencies ??= new ModuleDependencyCollection();
    }
}
