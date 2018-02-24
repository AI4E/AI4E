/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IModuleMetadata.cs
 * Types:           (1) AI4E.Modularity.IModuleMetadata
 *                  (2) AI4E.Modularity.IModuleDependency
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
 * (1) Introduce a property that specifies how the module can be loaded.
 * (2) Introduce a / multiple platform (host) dependencies.
 * (3) Group dependencies by platform.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

namespace AI4E.Modularity
{
    /// <summary>
    /// Represents metadata describing a module.
    /// </summary>
    public interface IModuleMetadata
    {
        #region Description Properties

        /// <summary>
        /// Gets the module name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the module version.
        /// </summary>
        ModuleVersion Version { get; }

        /// <summary>
        /// Gets the date and time of the module release.
        /// </summary>
        DateTime ReleaseDate { get; }

        /// <summary>
        /// Gets the modules descriptive name.
        /// </summary>
        string DescriptiveName { get; }

        /// <summary>
        /// Gets the module description.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the module icon.
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

        #endregion

        #region Loader properties

        ///// <summary>
        ///// Gets the relative path of the modules entry assembly.
        ///// </summary>
        //string EntryAssemblyPath { get; }

        string EntryAssemblyCommand { get; }

        string EntryAssemblyArguments { get; }

        /// <summary>
        /// Gets a collection of the modules dependencies.
        /// </summary>
        ICollection<IModuleDependency> Dependencies { get; }

        #endregion
    }

    /// <summary>
    /// Describes a modules dependency.
    /// </summary>
    public interface IModuleDependency
    {
        /// <summary>
        /// The dependency module name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A version filter the dependency modules version must match.
        /// </summary>
        ModuleVersionFilter Version { get; }
    }
}
