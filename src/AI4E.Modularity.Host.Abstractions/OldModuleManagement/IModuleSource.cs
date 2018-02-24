/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IModuleSource.cs
 * Types:           AI4E.Modularity.IModuleSource
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

namespace AI4E.Modularity
{
    /// <summary>
    /// Represents a module source that modules can be loaded from.
    /// </summary>
    public interface IModuleSource
    {
        /// <summary>
        /// Gets the name of the module source.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the path or uri of the module source.
        /// </summary>
        string Source { get; }
    }
}
