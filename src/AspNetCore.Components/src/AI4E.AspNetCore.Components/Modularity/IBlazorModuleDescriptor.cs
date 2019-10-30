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

using System.Collections.Immutable;

namespace AI4E.AspNetCore.Components.Modularity
{
    /// <summary>
    /// Describes a single blazor-module.
    /// </summary>
    public interface IBlazorModuleDescriptor
    {
        /// <summary>
        /// Gets a collection of <see cref="IBlazorModuleAssemblyDescriptor"/> 
        /// describing the assemblies the module contains of.
        /// </summary>
        ImmutableList<IBlazorModuleAssemblyDescriptor> Assemblies { get; }

        /// <summary>
        /// Gets the module name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the url that the module's assemblies can be requested from.
        /// </summary>
        string UrlPrefix { get; }
    }
}