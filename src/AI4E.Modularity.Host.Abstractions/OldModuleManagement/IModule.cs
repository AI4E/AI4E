/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IModule.cs
 * Types:           AI4E.Modularity.IModule
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   01.10.2017 
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

/* TODO
 * --------------------------------------------------------------------------------------------------------------------
 * (1) A debug module should be a module release not a module.
 * (2) A debug module should offer the posibility of metadata like description, icon, etc.
 * (3) Remove the bool results from Install and Uninstall.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    /// <summary>
    /// Represents a module.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Gets the modules unique identifier.
        /// </summary>
        ModuleIdentifier Identifier { get; }

        /// <summary>
        /// Gets the release date of the latest release or null if the module is a debug session.
        /// </summary>
        DateTime? ReleaseDate { get; }

        /// <summary>
        /// Gets the descriptive name of the latest release or null if the module is a debug session.
        /// </summary>
        string DescriptiveName { get; }

        /// <summary>
        /// Gets the description of the latest release or null if the module is a debug session.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon of the latest release or <see cref="ModuleIcon.NoIcon"/> if the module is a debug session.
        /// </summary>
        ModuleIcon Icon { get; }

        /// <summary>
        /// Gets the author of the latest release or null if the module is a debug session.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Gets the modules reference uri or null if the module is a debug session.
        /// </summary>
        string ReferencePageUri { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the module is a debug session.
        /// </summary>
        bool IsDebugModule { get; }

        /// <summary>
        /// Gets a collection of releases of the module.
        /// </summary>
        IEnumerable<IModuleRelease> Releases { get; }

        /// <summary>
        /// Gets the latest release of the module.
        /// </summary>
        IModuleRelease LatestRelease { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the module is currently installed.
        /// </summary>
        bool IsInstalled { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the latest release of the module is installed.
        /// </summary>
        bool IsLatestReleaseInstalled { get; }

        /// <summary>
        /// Asynchronously installs the latest release of the module.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InstallAsync();

        /// <summary>
        /// Asynchronously uninstalls the module.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UninstallAsync();
    }
}
