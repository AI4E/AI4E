/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Threading;

namespace AI4E
{
    public sealed class AssemblySource : IAssemblySource
    {
        private ImmutableDictionary<Assembly, AssemblyContext> _assemblies;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="AssemblyRegistry"/> type.
        /// </summary>
        internal AssemblySource(
            ImmutableDictionary<Assembly, AssemblyContext> assemblies, 
            IServiceProvider serviceProvider)
        {
            _assemblies = assemblies;
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public IReadOnlyCollection<Assembly> Assemblies
        {
            get
            {
                var assemblies = Volatile.Read(ref _assemblies);
                var keys = assemblies.Keys;

                return (keys as IReadOnlyCollection<Assembly>) ?? keys.ToImmutableList();
            }
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

        private AssemblyContext GetAssemblyContext(Assembly assembly)
        {
            var assemblies = Volatile.Read(ref _assemblies);

            if (assemblies.TryGetValue(assembly, out var assemblyContext))
            {
                return assemblyContext;
            }

            return AssemblyContext.Empty;
        }

        /// <inheritdoc />
        public AssemblyLoadContext GetAssemblyLoadContext(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            return GetAssemblyContext(assembly).AssemblyLoadContext ?? AssemblyLoadContext.Default;
        }

        /// <inhertdoc />
        public IServiceProvider GetAssemblyServiceProvider(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            return GetAssemblyContext(assembly).AssemblyServiceProvider ?? _serviceProvider;
        }

        /// <inheritdoc />
        public bool ContainsAssembly(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            var assemblies = Volatile.Read(ref _assemblies);

            return assemblies.ContainsKey(assembly);
        }
    }
}
