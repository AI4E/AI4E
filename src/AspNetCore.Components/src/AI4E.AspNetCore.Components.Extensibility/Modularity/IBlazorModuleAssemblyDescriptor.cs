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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.AspNetCore.Components.Modularity
{
    /// <summary>
    /// Describes a single assembly of a blazor module.
    /// </summary>
    public interface IBlazorModuleAssemblyDescriptor
    {
        /// <summary>
        /// Gets the <see cref="IBlazorModuleDescriptor"/> describing the module the assembly belongs to.
        /// </summary>
        IBlazorModuleDescriptor ModuleDescriptor { get; }

        /// <summary>
        /// Gets the assembly name.
        /// </summary>
        string AssemblyName { get; }

        /// <summary>
        /// Gets the assembly version.
        /// </summary>
        Version AssemblyVersion { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the assembly contains blazor-components.
        /// </summary>
        bool IsComponentAssembly { get; }

        /// <summary>
        /// Asynchronously loads the assembly source.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operator.
        /// When evaluated, the tasks result contains the <see cref="BlazorModuleAssemblySource"/>.
        /// </returns>
        ValueTask<BlazorModuleAssemblySource> LoadAssemblySourceAsync(CancellationToken cancellation = default);
    }
}