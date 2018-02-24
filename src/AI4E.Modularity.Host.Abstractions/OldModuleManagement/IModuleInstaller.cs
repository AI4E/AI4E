/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IModuleInstaller.cs
 * Types:           (1) AI4E.Modularity.IModuleInstaller
 *                  (2) AI4E.Modularity.IModuleInstallation
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   22.10.2017 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    /// <summary>
    /// Describes a module installer.
    /// </summary>
    public interface IModuleInstaller
    {
        /// <summary>
        /// Get a collection of installed modules.
        /// </summary>
        IReadOnlyCollection<IModuleInstallation> InstalledModules { get; }

        IReadOnlyCollection<IModuleSource> ModuleSources { get; }

        /// <summary>
        /// Asynchronously installs the module specified by its identifier.
        /// </summary>
        /// <param name="module">The identifier of the module release.</param>
        /// <param name="source">The module source, the module shall be loaded from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="System.ArgumentException">Thrown if <paramref name="module"/> equals <see cref="ModuleReleaseIdentifier.UnknownModuleRelease"/>.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
        /// <exception cref="ModuleInstallationException">Thrown if the specified module could not be installed.</exception>
        Task InstallAsync(ModuleReleaseIdentifier module, IModuleSource source);

        /// <summary>
        /// Asynchronously uninstalls the module specified by its identifier.
        /// </summary>
        /// <param name="module">The module that shall be uninstalled.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="System.ArgumentException">Thrown if <paramref name="module"/> equals <see cref="ModuleIdentifier.UnknownModule"/>.</exception>
        /// <exception cref="ModuleUninstallationException">Thrown if the module is currently installed but cannot be uninstalled.</exception>
        Task UninstallAsync(ModuleIdentifier module);

        Task AddModuleSourceAsync(string name, string source);

        Task RemoveModuleSourceAsync(string name);

        Task UpdateModuleSourceAsync(string name, string source);

        IModuleSource GetModuleSource(string name);

        IModuleLoader GetModuleLoader(IModuleSource moduleSource);
    }

    /// <summary>
    /// Describes a module installation.
    /// </summary>
    public interface IModuleInstallation
    {
        /// <summary>
        /// Gets the module source the module was loaded from.
        /// </summary>
        IModuleSource Source { get; }

        /// <summary>
        /// Gets the modules unique identifier.
        /// </summary>
        ModuleIdentifier Module { get; }

        /// <summary>
        /// Gets the installed module version.
        /// </summary>
        ModuleVersion Version { get; }

        /// <summary>
        /// Gets the modules metadata.
        /// </summary>
        IModuleMetadata ModuleMetadata { get; }

        /// <summary>
        /// Get the installation directory.
        /// </summary>
        string InstallationDirectory { get; }
    }
}