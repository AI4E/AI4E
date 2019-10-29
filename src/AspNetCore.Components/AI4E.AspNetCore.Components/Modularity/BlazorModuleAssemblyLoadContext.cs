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
using System.Reflection;
using System.Runtime.Loader;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class BlazorModuleAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly ImmutableDictionary<AssemblyName, Assembly> _coreAssemblies;
        private readonly ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> _assemblySources;
        private readonly Dictionary<AssemblyName, Assembly> _assemblyCache;

        public BlazorModuleAssemblyLoadContext(
            ImmutableDictionary<AssemblyName, Assembly> coreAssemblies,
            ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> assemblySources)

        // TODO: Either remove the multi-targeting, or add a shim for this.
#if SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT
                : base(isCollectible: true)
#endif
        {
            if (coreAssemblies is null)
                throw new ArgumentNullException(nameof(coreAssemblies));

            if (assemblySources is null)
                throw new ArgumentNullException(nameof(assemblySources));

            _coreAssemblies = coreAssemblies;
            _assemblySources = assemblySources;
            _assemblyCache = new Dictionary<AssemblyName, Assembly>(AssemblyNameComparer.Instance);

            Unloading += OnUnloading;
        }

        private void OnUnloading(AssemblyLoadContext obj)
        {
            Unloading -= OnUnloading;

            foreach (var source in _assemblySources!.Values)
            {
                source.Dispose();
            }
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (_coreAssemblies.TryGetValue(assemblyName, out var assembly)
                || _assemblyCache.TryGetValue(assemblyName, out assembly!))
            {
                return assembly;
            }

            if (!_assemblySources.TryGetValue(assemblyName, out var assemblySource))
            {
                return null;
            }

            assembly = assemblySource.Load(this);
            _assemblyCache.Add(assemblyName, assembly);
            return assembly;
        }
    }
}
