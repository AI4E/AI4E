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

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// An attribute that marks the decorated type or member as projection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ProjectionAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the type of projection target.
        /// </summary>
        public Type TargetType { get; set; }

        /// <summary>
        /// Gets or sets the type of entity.
        /// </summary>
        public Type EntityType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the projection projects to multiple targets.
        /// </summary>
        public MultipleProjectionResults MultipleResults { get; set; }

        /// <summary>
        /// Gets or sets a boolean value indicating whether the projection shall be invoked for non-existing entities.
        /// </summary>
        public bool ProjectNonExisting { get; set; }
    }

    public enum MultipleProjectionResults
    {
        Unset = 0,
        SingleResult = 1,
        MultipleResults = 2
    }
}
