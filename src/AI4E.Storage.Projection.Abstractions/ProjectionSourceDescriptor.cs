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
    /// Describes a projection source.
    /// </summary>
    public readonly struct ProjectionSourceDescriptor : IEquatable<ProjectionSourceDescriptor>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionSourceDescriptor"/> type.
        /// </summary>
        /// <param name="sourceType">The type of the projection source.</param>
        /// <param name="sourceId">The id of the projection source.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="sourceType"/> or <paramref name="sourceId"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="sourceId"/> is empty or consists of whitespace only.</exception>
        public ProjectionSourceDescriptor(Type sourceType, string sourceId)
        {
            if (sourceType is null)
                throw new ArgumentNullException(nameof(sourceType));

            if (sourceId is null)
                throw new ArgumentNullException(nameof(sourceId));

            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("The value must neither be empty, nor consist of whitespace only.", nameof(sourceId));

            SourceType = sourceType;
            SourceId = sourceId;
        }

        /// <summary>
        /// Gets the type of the projection source.
        /// </summary>
        public Type SourceType { get; }

        /// <summary>
        /// Gets the id of the projection source.
        /// </summary>
        public string SourceId { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is ProjectionSourceDescriptor entityDescriptor && Equals(entityDescriptor);
        }

        /// <inheritdoc/>
        public bool Equals(ProjectionSourceDescriptor other)
        {
            if (other.SourceType is null && SourceType is null)
                return true;

            return (other.SourceType, other.SourceId) == (SourceType, SourceId);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (SourceType is null)
                return 0;

            return (SourceType, SourceId).GetHashCode();
        }

        /// <summary>
        /// Checks two instances of <see cref="ProjectionSourceDescriptor"/> to be equal.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in ProjectionSourceDescriptor left, in ProjectionSourceDescriptor right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks two instances of <see cref="ProjectionSourceDescriptor"/> to be non-equal.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in ProjectionSourceDescriptor left, in ProjectionSourceDescriptor right)
        {
            return !left.Equals(right);
        }
    }
}
