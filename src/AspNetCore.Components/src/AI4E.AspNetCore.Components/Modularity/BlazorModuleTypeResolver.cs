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
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class BlazorModuleTypeResolver : TypeResolver
    {
        private readonly BlazorModuleAssemblyLoadContext _moduleLoadContext;

        public BlazorModuleTypeResolver(BlazorModuleAssemblyLoadContext moduleLoadContext)
        {
            if (moduleLoadContext is null)
                throw new ArgumentNullException(nameof(moduleLoadContext));

            _moduleLoadContext = moduleLoadContext;
        }

        private ImmutableHashSet<Assembly> Assemblies => _moduleLoadContext.InstalledAssemblies;

        protected override IEnumerable<Assembly> ReflectAssemblies()
        {
            var defaultAssemblies = (IEnumerable<Assembly>)GetDefaultContextAssemblies();

            // This is a common case, so we optimize for this.
            if (!Assemblies.Any())
            {
                return defaultAssemblies;
            }

            // Get all assemblies from defaultAssemblies that is not present in _assemblies already.
            defaultAssemblies = defaultAssemblies.Where(assembly => !Assemblies.Contains(assembly));

            return Assemblies     
                .ToImmutableList()
                .AddRange(defaultAssemblies);
        }
    }
}
