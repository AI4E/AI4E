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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.AspNetCore.Components.Modularity
{
    /// <summary>
    /// Manages the installation of blazor modules.
    /// </summary>
    /// <remarks>
    /// Implementors do not need to guarantee thread-safety for instance members.
    /// </remarks>
    public interface IBlazorModuleManager : IDisposable
    {
        /// <summary>
        /// Asynchronously installs the specified blazor-module.
        /// </summary>
        /// <param name="moduleDescriptor">
        /// The <see cref="IBlazorModuleDescriptor"/> that specifies the blazor-module to install.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operator,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the module actually needed
        /// to be installed.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="moduleDescriptor"/> is <c>null</c>.
        /// </exception>
        ValueTask<bool> InstallAsync(IBlazorModuleDescriptor moduleDescriptor, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously uninstalls the specified blazor-module.
        /// </summary>
        /// <param name="moduleDescriptor">
        /// The <see cref="IBlazorModuleDescriptor"/> that specifies the blazor-module to uninstall.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operator,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the module actually needed
        /// to be uninstalled.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="moduleDescriptor"/> is <c>null</c>.
        /// </exception>
        ValueTask<bool> UninstallAsync(IBlazorModuleDescriptor moduleDescriptor, CancellationToken cancellation = default);

        /// <summary>
        /// Returns a boolean value indicating whether the specified blazor-module is installed.
        /// </summary>
        /// <param name="moduleDescriptor">
        /// The <see cref="IBlazorModuleDescriptor"/> that specifies the blazor-module 
        /// thats installation status shall be returned.
        /// </param>
        /// <returns>True if <paramref name="moduleDescriptor"/> is installed, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="moduleDescriptor"/> is <c>null</c>.
        /// </exception>
        bool IsInstalled(IBlazorModuleDescriptor moduleDescriptor);

        /// <summary>
        /// Returns a collection of descriptors of all installed blazor-modules.
        /// </summary>
        IEnumerable<IBlazorModuleDescriptor> InstalledModules { get; }
    }
}
