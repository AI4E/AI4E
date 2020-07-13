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

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Represents the result of a projection.
    /// </summary>
    public interface IProjectionResult
    {
        /// <summary>
        /// Gets the projection result id.
        /// </summary>
        object ResultId { get; }

        /// <summary>
        /// Gets the projection result.
        /// </summary>
        object Result { get; }

        /// <summary>
        /// Gets the type of id of the projection result.
        /// </summary>
        Type ResultIdType { get; }

        /// <summary>
        /// Gets the projection result type.
        /// </summary>
        Type ResultType { get; }

        /// <summary>
        /// Returns the projection target descriptor that describes the projection result.
        /// </summary>
        /// <returns>A <see cref="ProjectionTargetDescriptor"/> that describes the projection result.</returns>
        public ProjectionTargetDescriptor AsTargetDescriptor()
        {
            return ProjectionResultExtension.AsTargetDescriptor(this);
        }
    }

    /// <summary>
    /// Contains extensions for the <see cref="IProjectionResult"/> type.
    /// </summary>
    public static class ProjectionResultExtension
    {
        /// <summary>
        /// Returns the projection target descriptor that describes the projection result.
        /// </summary>
        /// <param name="projectionResult">The projection result.</param>
        /// <returns>A <see cref="ProjectionTargetDescriptor"/> that describes the projection result.</returns>
        public static ProjectionTargetDescriptor AsTargetDescriptor(
            this IProjectionResult projectionResult)
        {
            return new ProjectionTargetDescriptor(
                projectionResult.ResultType,
                projectionResult.ResultId.ToString());
        }
    }
}
