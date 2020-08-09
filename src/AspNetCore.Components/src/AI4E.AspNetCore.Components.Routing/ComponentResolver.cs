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
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using AI4E.Utils;
using Microsoft.AspNetCore.Components;

namespace AI4E.AspNetCore.Components
{
    /// <summary>
    /// Resolves components for an application.
    /// </summary>
    public static class ComponentResolver
    {
        /// <summary>
        /// Gets the assembly that defines the <see cref="IComponent"/> interface.
        /// </summary>
        public static Assembly BlazorAssembly { get; } = typeof(IComponent).Assembly; // TODO: Rename

        /// <summary>
        /// Returns all types of components that are defined in the specified assembly or a dependency assembly.
        /// </summary>
        /// <param name="assembly">The origin assembly.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of component types.</returns>
        public static IEnumerable<Type> ResolveComponents(Assembly assembly)
        {
            return EnumerateComponentAssemblies(assembly).SelectMany(a => GetComponents(a));
        }

        /// <summary>
        /// Returns all types of components that are defined in the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly that contains the component types.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of component types.</returns>
        public static IEnumerable<Type> GetComponents(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            return assembly.ExportedTypes.Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsInterface);
        }

        /// <summary>
        /// Enumerates all assemblies that are dependencies of the specified assembly and contain components.
        /// </summary>
        /// <param name="assembly">The origin assembly.</param>
        /// <param name="loadContext">The <see cref="AssemblyLoadContext"/> the origin assembly was loaded from.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of assemblies that contain components.</returns>
        public static IEnumerable<Assembly> EnumerateComponentAssemblies(Assembly assembly, AssemblyLoadContext? loadContext = null)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            var assemblyName = assembly.GetName();
            var visited = new HashSet<Assembly>(new AssemblyComparer());
            return EnumerateAssemblies(assemblyName, loadContext, visited);
        }

        private static IEnumerable<Assembly> EnumerateAssemblies(
            AssemblyName assemblyName,
            AssemblyLoadContext? loadContext,
            HashSet<Assembly> visited)
        {
            Assembly assembly;

            if (loadContext == null)
            {
                assembly = Assembly.Load(assemblyName);
            }
            else
            {
                assembly = loadContext.LoadFromAssemblyName(assemblyName);
            }

            if (visited.Contains(assembly))
            {
                // Avoid traversing visited assemblies.
                yield break;
            }

            visited.Add(assembly);
            var references = assembly.GetReferencedAssemblies();
            if (!references.Any(r => AssemblyNameComparer.BySimpleName.Equals(r, BlazorAssembly.GetName())))
            {
                // Avoid traversing references that don't point to blazor (like netstandard2.0)
                yield break;
            }

            yield return assembly;

            // Look at the list of transitive dependencies for more components.
            foreach (var reference in references.SelectMany(r => EnumerateAssemblies(r, loadContext, visited)))
            {
                yield return reference;
            }

        }

        private class AssemblyComparer : IEqualityComparer<Assembly>
        {
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
    }
}
