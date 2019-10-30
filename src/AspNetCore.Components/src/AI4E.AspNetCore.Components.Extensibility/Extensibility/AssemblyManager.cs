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

// TODO: This type should be thread-safe.


using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace AI4E.AspNetCore.Components.Extensibility
{
    /// <summary>
    /// Manages a set of assemblies.
    /// </summary>
    public class AssemblyManager : IAssemblySource
    {
        private readonly Dictionary<Assembly, AssemblyLoadContext?> _assemblies
            = new Dictionary<Assembly, AssemblyLoadContext?>();

        /// <summary>
        /// Creates a new instance of the <see cref="AssemblyManager"/> type.
        /// </summary>
        public AssemblyManager() { }

        /// <summary>
        /// Creates a new instance of the <see cref="AssemblyManager"/> type that
        /// initially contains all assemblies that are dependencies of the specified
        /// assembly and contain components.
        /// </summary>
        /// <param name="entryAssembly">The entry assembly.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entryAssembly"/> is <c>null</c>.
        /// </exception>
        public AssemblyManager(Assembly entryAssembly)
        {
            if (entryAssembly == null)
                throw new ArgumentNullException(nameof(entryAssembly));

            var assemblies = ComponentResolver.EnumerateComponentAssemblies(entryAssembly);
            foreach (var assembly in assemblies)
            {
                _assemblies.Add(assembly, null);
            }
        }

        /// <inheritdoc />
        public IReadOnlyCollection<Assembly> Assemblies => _assemblies.Keys.ToImmutableList();

        /// <inheritdoc />
        public event IAssemblySource.AssembliedChangedEventHandler? AssembliesChanged;

        /// <summary>
        /// Adds an assembly to the manager.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/> to add.</param>
        /// <param name="assemblyLoadContext">
        /// The <see cref="AssemblyLoadContext"/> that <paramref name="assembly"/> was loaded from, or <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        public ValueTask AddAssemblyAsync(Assembly assembly, AssemblyLoadContext? assemblyLoadContext = null)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            if (!AddCore(assembly, assemblyLoadContext))
            {
                return default;
            }

            return NotifyAssembliesChangedAsync();
        }

        public ValueTask AddAssembliesAsync(IEnumerable<Assembly> assemblies, AssemblyLoadContext? assemblyLoadContext = null)
        {
            if (assemblies is null)
                throw new ArgumentNullException(nameof(assemblies));

            var changed = false;

            foreach (var assembly in assemblies)
            {
                if (assembly is null)
                {
                    throw new ArgumentException("The collection must not contain null entries.", nameof(assemblies));
                }

                changed |= AddCore(assembly, assemblyLoadContext);
            }

            return changed ? NotifyAssembliesChangedAsync() : default;
        }

        private bool AddCore(Assembly assembly, AssemblyLoadContext? assemblyLoadContext)
        {
            if (_assemblies.TryGetValue(assembly, out var comparisonALC)
               && comparisonALC == assemblyLoadContext)
            {
                return false;
            }

            _assemblies[assembly] = assemblyLoadContext;
            return true;
        }

        /// <summary>
        /// Removes an assembly from the manager.
        /// </summary>
        /// <param name="assembly">The assembly to remove.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        public ValueTask RemoveAssemblyAsync(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            if (!_assemblies.Remove(assembly))
            {
                return default;
            }

            return NotifyAssembliesChangedAsync();
        }

        public ValueTask RemoveAssembliesAsync(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            var changed = false;

            foreach (var assembly in assemblies)
            {
                if (assembly is null)
                {
                    throw new ArgumentException("The collection must not contain null entries.", nameof(assemblies));
                }

                changed |= _assemblies.Remove(assembly);
            }

            return changed ? NotifyAssembliesChangedAsync() : default;
        }

        private ValueTask NotifyAssembliesChangedAsync()
        {
            var assemblies = Assemblies;
            return AssembliesChanged?.InvokeAllAsync(handler => handler(this, assemblies)) ?? default;
        }

        /// <inheritdoc />
        public bool CanUnload(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            var assemblyLoadContext = GetAssemblyLoadContext(assembly);

            if (assemblyLoadContext is null)
            {
                return false;
            }

#if SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT
            return assemblyLoadContext.IsCollectible;
#else
            return true;
#endif
        }

        /// <inheritdoc />
        public AssemblyLoadContext? GetAssemblyLoadContext(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            if (!_assemblies.TryGetValue(assembly, out var assemblyLoadContext))
            {
                assemblyLoadContext = null;
            }

            return assemblyLoadContext;
        }

        /// <inheritdoc />
        public bool ContainsAssembly(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            return _assemblies.ContainsKey(assembly);
        }
    }
}

