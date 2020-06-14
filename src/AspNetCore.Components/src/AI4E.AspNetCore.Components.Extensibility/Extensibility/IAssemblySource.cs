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
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace AI4E.AspNetCore.Components.Extensibility
{
    /// <summary>
    /// Represents a lookup for assembly that components can be load from.
    /// </summary>
    public interface IAssemblySource
    {
        /// <summary>
        /// Gets all known assemblies that contain components.
        /// </summary>
        IReadOnlyCollection<Assembly> Assemblies { get; }

        /// <summary>
        /// Notifies that the <see cref="Assemblies"/> collection changed.
        /// </summary>
        event AssembliedChangedEventHandler? AssembliesChanged;

        /// <summary>
        /// Returns a boolean value indicating whether the specified assembly may unload.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/> to check.</param>
        /// <returns>A boolean value indicating whether <paramref name="assembly"/> is may unload.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        bool CanUnload(Assembly assembly);

        /// <summary>
        /// Returns the <see cref="AssemblyLoadContext"/> that the specified assembly was loaded from.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/>.</param>
        /// <returns>The <see cref="AssemblyLoadContext"/> that <paramref name="assembly"/> was loaded from,
        /// or <c>null</c> if the <see cref="AssemblyLoadContext"/> of <paramref name="assembly"/> is not available.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        AssemblyLoadContext? GetAssemblyLoadContext(Assembly assembly);

        /// <summary>
        /// Returns the <see cref="IServiceProvider"/> that is responsible to load services from the specified assembly.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/>.</param>
        /// <returns>
        /// The <see cref="IServiceProvider"/> that is responsible to load services from the specified assembly,
        /// or <c>null</c> if no service provider is registered.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        IServiceProvider? GetAssemblyServiceProvider(Assembly assembly);

        /// <summary>
        /// Returns a boolean value indicating whether the specified assembly is contained in the assembly source.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/>.</param>
        /// <returns>A boolean value indicating whether <paramref name="assembly"/> is contained in the current assembly source.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        bool ContainsAssembly(Assembly assembly);

        public delegate ValueTask AssembliedChangedEventHandler(
            IAssemblySource sender,
            IReadOnlyCollection<Assembly> assemblies);
    }
}
