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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * AspNet Core (https://github.com/aspnet/AspNetCore)
 * Copyright (c) .NET Foundation. All rights reserved.
 * Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using AI4E.Utils;

namespace AI4E
{
    public interface IAssemblyResolver
    {
        public IEnumerable<Assembly> EnumerateAssemblies(Assembly assembly, AssemblyLoadContext? loadContext = null);

        public ImmutableHashSet<Assembly> GetAssemblies(Assembly assembly, AssemblyLoadContext? loadContext = null)
        {
            // By reference comparison is ok for this case.
            return EnumerateAssemblies(assembly, loadContext).ToImmutableHashSet(AssemblyComparer.Instance);
        }
    }

    public abstract class AssemblyResolverBase : IAssemblyResolver
    {
        public virtual IEnumerable<Assembly> EnumerateAssemblies(
            Assembly assembly,
            AssemblyLoadContext? loadContext = null)
        {
            return GetAssemblies(assembly, loadContext);
        }

        public ImmutableHashSet<Assembly> GetAssemblies(
            Assembly assembly,
            AssemblyLoadContext? loadContext = null)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            var assemblyName = assembly.GetName();
            var visited = new HashSet<Assembly>(AssemblyComparer.Instance);
            var resultBuilder = ImmutableHashSet.CreateBuilder(AssemblyComparer.Instance);

            loadContext ??= AssemblyLoadContext.Default;

            EnumerateAssemblies(assemblyName, loadContext, visited, resultBuilder);

            return resultBuilder.ToImmutable();
        }

        private void EnumerateAssemblies(
           AssemblyName assemblyName,
           AssemblyLoadContext loadContext,
           HashSet<Assembly> visited,
           ImmutableHashSet<Assembly>.Builder resultBuilder)
        {
            var assembly = loadContext.LoadFromAssemblyName(assemblyName);

            if (visited.Contains(assembly))
            {
                // Avoid traversing visited assemblies.
                return;
            }

            visited.Add(assembly);

            if (!MatchesCondition(assembly))
            {
                return;
            }

            resultBuilder.Add(assembly);

            var references = assembly.GetReferencedAssemblies();

            // Look at the list of transitive dependencies for more assemblies.
            foreach (var reference in references)
            {
                EnumerateAssemblies(reference, loadContext, visited, resultBuilder);
            }
        }

        protected abstract bool MatchesCondition(Assembly assembly);
    }
    internal sealed class AssemblyComparer : IEqualityComparer<Assembly>
    {
        public static AssemblyComparer Instance { get; } = new AssemblyComparer();

        private AssemblyComparer() { }

        public bool Equals(Assembly? x, Assembly? y)
        {
            return string.Equals(x?.FullName, y?.FullName, StringComparison.Ordinal);
        }

        public int GetHashCode(Assembly obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            return obj.FullName?.GetHashCode(StringComparison.Ordinal) ?? 0;
        }
    }

    public sealed class HasReferenceAssemblyResolver : AssemblyResolverBase
    {
        private readonly AssemblyName _referenceName;
        private readonly AssemblyNameComparer _comparer;

        public HasReferenceAssemblyResolver(AssemblyName referenceName, AssemblyNameComparer comparer)
        {
            if (referenceName is null)
                throw new ArgumentNullException(nameof(referenceName));

            if (comparer is null)
                throw new ArgumentNullException(nameof(comparer));

            _referenceName = referenceName;
            _comparer = comparer;
        }

        protected override bool MatchesCondition(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().Any(r => _comparer.Equals(r, _referenceName));
        }
    }
}
