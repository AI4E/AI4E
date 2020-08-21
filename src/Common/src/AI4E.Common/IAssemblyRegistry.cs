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
using System.Reflection;
using System.Runtime.Loader;

namespace AI4E
{
    public interface IAssemblyRegistry
    {
        public void AddAssemblies(
            IEnumerable<Assembly> assemblies,
            AssemblyLoadContext? assemblyLoadContext = null,
            IServiceProvider? assemblyServiceProvider = null);

        /// <summary>
        /// Adds an assembly to the manager.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/> to add.</param>
        /// <param name="assemblyLoadContext">
        /// The <see cref="AssemblyLoadContext"/> that <paramref name="assembly"/> was loaded from, or <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        public void AddAssembly(
            Assembly assembly,
            AssemblyLoadContext? assemblyLoadContext = null,
            IServiceProvider? assemblyServiceProvider = null)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            AddAssemblies(assembly.Yield(), assemblyLoadContext, assemblyServiceProvider);
        }

        public void RemoveAssemblies(IEnumerable<Assembly> assemblies);

        /// <summary>
        /// Removes an assembly from the manager.
        /// </summary>
        /// <param name="assembly">The assembly to remove.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        public void RemoveAssembly(Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            RemoveAssemblies(assembly.Yield());
        }

        IAssemblySource AssemblySource { get; }

        event EventHandler? AssemblySourceChanged;
    }
}