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
    /// Describes a projection target.
    /// </summary>
    public readonly struct ProjectionTargetDescriptor : IEquatable<ProjectionTargetDescriptor>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionTargetDescriptor"/> type.
        /// </summary>
        /// <param name="targetType">The type of the projection target.</param>
        /// <param name="targetId">The id of the projection target.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="targetType"/> or <paramref name="targetType"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="targetId"/> is empty or consists of whitespace only.</exception>
        public ProjectionTargetDescriptor(Type targetType, string targetId)
        {
            if (targetType is null)
                throw new ArgumentNullException(nameof(targetType));

            if (targetId is null)
                throw new ArgumentNullException(nameof(targetId));

            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("The value must neither be empty, nor consist of whitespace only.", nameof(targetId));

            TargetType = targetType;
            TargetId = targetId;
        }

        /// <summary>
        /// Gets the type of the projection target.
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// Gets the id of the projection target.
        /// </summary>
        public string TargetId { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is ProjectionTargetDescriptor entityDescriptor && Equals(entityDescriptor);
        }

        /// <inheritdoc/>
        public bool Equals(ProjectionTargetDescriptor other)
        {
            if (other.TargetType is null && TargetType is null)
                return true;

            return (other.TargetType, other.TargetId) == (TargetType, TargetId);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (TargetType is null)
                return 0;

            return (TargetType, TargetId).GetHashCode();
        }

        /// <summary>
        /// Checks two instances of <see cref="ProjectionTargetDescriptor"/> to be equal.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in ProjectionTargetDescriptor left, in ProjectionTargetDescriptor right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks two instances of <see cref="ProjectionTargetDescriptor"/> to be non-equal.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in ProjectionTargetDescriptor left, in ProjectionTargetDescriptor right)
        {
            return !left.Equals(right);
        }
    }
}
