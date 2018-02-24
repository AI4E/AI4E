/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IModuleRelease.cs
 * Types:           AI4E.Modularity.IModuleRelease
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
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    /// <summary>
    /// Represents a module release.
    /// </summary>
    public interface IModuleRelease
    {
        /// <summary>
        /// Gets the unique identifier of the module release.
        /// </summary>
        ModuleReleaseIdentifier Identifier { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the module is a pre-release.
        /// </summary>
        bool IsPreRelease { get; }

        /// <summary>
        /// Gets the version of the module release.
        /// </summary>
        ModuleVersion Version { get; }

        /// <summary>
        /// Gets the release date of the module.
        /// </summary>
        DateTime ReleaseDate { get; }

        /// <summary>
        /// Gets the descriptive name of the module.
        /// </summary>
        string DescriptiveName { get; }

        /// <summary>
        /// Gets the moduel description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon of the module.
        /// </summary>
        ModuleIcon Icon { get; }

        /// <summary>
        /// Gets the module author.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Gets a uri to a website describing or presenting the module.
        /// </summary>
        string ReferencePageUri { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the release of the module is installed currently.
        /// </summary>
        bool IsInstalled { get; }

        /// <summary>
        /// Asynchronously installs the release of the module.
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
