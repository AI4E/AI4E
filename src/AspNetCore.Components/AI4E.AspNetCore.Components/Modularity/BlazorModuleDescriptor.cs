﻿/* License
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
using System.Collections.Immutable;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleDescriptor : IBlazorModuleDescriptor
    {
        public BlazorModuleDescriptor(
            string name,
            ImmutableList<IBlazorModuleAssemblyDescriptor> assemblies,
            ImmutableList<string> dependencies,
            string urlPrefix) // TODO: Rename
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            if (assemblies is null)
                throw new ArgumentNullException(nameof(assemblies));

            if (dependencies is null)
                throw new ArgumentNullException(nameof(dependencies));

            if (urlPrefix is null)
                throw new ArgumentNullException(nameof(urlPrefix));

            Name = name;
            Assemblies = assemblies;
            Dependencies = dependencies;
            UrlPrefix = urlPrefix;
        }

        public string Name { get; }
        public ImmutableList<IBlazorModuleAssemblyDescriptor> Assemblies { get; }
        public ImmutableList<string> Dependencies { get; }
        public string UrlPrefix { get; }
    }
}