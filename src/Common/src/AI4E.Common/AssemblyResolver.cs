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
using System.Linq;
using System.Reflection;
using AI4E.Utils;

namespace AI4E.Storage.Domain.Projection
{
    public sealed class AssemblyResolver : AssemblyResolverBase
    {
        private readonly AssemblyName _rootAssembly;
        private readonly bool _includeAssemblyDependencies;
        private readonly ImmutableHashSet<AssemblyName> _exludedAssemblies;

        public AssemblyResolver(
            AssemblyName rootAssembly,
            bool includeAssemblyDependencies,
            IEnumerable<AssemblyName> exludedAssemblies)
        {
            if (rootAssembly is null)
                throw new ArgumentNullException(nameof(rootAssembly));

            if (exludedAssemblies is null)
                throw new ArgumentNullException(nameof(exludedAssemblies));

            _rootAssembly = (AssemblyName)rootAssembly.Clone();
            _includeAssemblyDependencies = includeAssemblyDependencies;

            static AssemblyName Clone(AssemblyName assemblyName)
            {
                if (assemblyName is null)
                    throw new ArgumentException(
                        "The argument must not contains null entries.", nameof(exludedAssemblies));

                return (AssemblyName)assemblyName.Clone();
            }

            _exludedAssemblies = exludedAssemblies
                .Select(Clone)
                .ToImmutableHashSet(AssemblyNameComparer.ByDisplayName);
        }

        protected override bool MatchesCondition(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            if (_exludedAssemblies.Contains(assembly.GetName()))
            {
                return false;
            }

            if (_includeAssemblyDependencies)
            {
                return true;
            }

            return AssemblyNameComparer.ByDisplayName.Equals(_rootAssembly, assembly.GetName());
        }
    }
}
