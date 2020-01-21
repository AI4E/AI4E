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
using System.Runtime.Loader;
using System.Threading;
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Modularity
{
    // TODO: Do we have to guarantee thread safety?
    internal sealed class BlazorModuleAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> _assemblySources;
        private readonly Dictionary<AssemblyName, Assembly> _assemblyCache; // TODO: Do we have to guarantee thread safety?
        private readonly ImmutableHashSet<AssemblyName> _coreAssemblies;

        private bool _unloading = false;

        public BlazorModuleAssemblyLoadContext(
            ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> assemblySources)

        // TODO: Either remove the multi-targeting, or add a shim for this.
#if SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT
                : base(isCollectible: true)
#endif
        {
            if (assemblySources is null)
                throw new ArgumentNullException(nameof(assemblySources));

            _assemblySources = assemblySources;
            _assemblyCache = new Dictionary<AssemblyName, Assembly>(AssemblyNameComparer.ByDisplayName);
            _coreAssemblies = BuildCoreAssemblies();

            Unloading += OnUnloading;
        }

        private void OnUnloading(AssemblyLoadContext obj)
        {
            Volatile.Write(ref _unloading, true);
            Unloading -= OnUnloading;
            _assemblyCache.Clear();

            foreach (var source in _assemblySources!.Values)
            {
                source.Dispose();
            }
        }

        public
#if NETCORE30 || NETCORE31
            new 
#endif
            IEnumerable<Assembly> Assemblies => _assemblyCache.Values;

        private ImmutableHashSet<AssemblyName> BuildCoreAssemblies()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(p => GetLoadContext(p) == Default)
                .Select(p => p.GetName())
                .Append(typeof(object).Assembly.GetName())
                .ToImmutableHashSet(AssemblyNameComparer.ByDisplayName);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (Volatile.Read(ref _unloading))
            {
                throw new Exception("Unable to load an assembly while unloading the assembly load context.");
            }

            Console.WriteLine($"Requested loading assembly {assemblyName}.");

            if (_assemblyCache.TryGetValue(assemblyName, out var assembly))
            {
                Console.WriteLine($"Found requested assembly {assemblyName} in cache.");

                return assembly;
            }

            var hasSource = _assemblySources.TryGetValue(assemblyName, out var assemblySource);

            // We have no source => Fallback to core or throw.
            if (!hasSource)
            {
                Console.WriteLine($"Requested assembly {assemblyName} has no source. Falling back to default context.");
                return null;
            }

            // We either have no source 
            // or we have a source but are not forces to load the assembly in the current context.
            if (!assemblySource.ForceLoad)
            {
                if (_coreAssemblies.Contains(assemblyName))
                {
                    Console.WriteLine($"Requested assembly {assemblyName} has matching core assembly. Falling back to core assembly.");
                    return null;
                }
            }

            Console.WriteLine($"Loding requested assembly {assemblyName} from source into current context.");

            assembly = assemblySource.Load(this);
            _assemblyCache.Add(assemblyName, assembly);
            return assembly;
        }
    }
}
