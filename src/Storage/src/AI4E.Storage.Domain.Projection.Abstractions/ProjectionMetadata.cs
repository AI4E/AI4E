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
using System.Collections.Immutable;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents the metadata of a projection.
    /// </summary>
    public readonly struct ProjectionMetadata
    {
        private readonly ImmutableList<ProjectionSourceDependency> _dependencies;
        private readonly ImmutableList<ProjectionTargetDescriptor> _targets;

        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionMetadata"/> type.
        /// </summary>
        /// <param name="dependencies">The projection dependencies.</param>
        /// <param name="targets">The projection targets.</param>
        /// <param name="projectionRevision">The projection revision.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="dependencies"/> or <paramref name="targets"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="projectionRevision"/> is negative.
        /// </exception>
        public ProjectionMetadata(
            IEnumerable<ProjectionSourceDependency> dependencies,
            IEnumerable<ProjectionTargetDescriptor> targets,
            long projectionRevision)
        {
            if (dependencies is null)
                throw new ArgumentNullException(nameof(dependencies));

            if (targets is null)
                throw new ArgumentNullException(nameof(targets));

            if (projectionRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(projectionRevision));

            _dependencies = (dependencies as ImmutableList<ProjectionSourceDependency>) ?? dependencies.ToImmutableList();
            _targets = (targets as ImmutableList<ProjectionTargetDescriptor>) ?? targets.ToImmutableList();
            ProjectionRevision = projectionRevision;
        }

        /// <summary>
        /// Gets the projection dependencies.
        /// </summary>
        public IReadOnlyCollection<ProjectionSourceDependency> Dependencies
            => _dependencies ?? ImmutableList<ProjectionSourceDependency>.Empty;

        /// <summary>
        /// Gets projection targets.
        /// </summary>
        public IReadOnlyCollection<ProjectionTargetDescriptor> Targets
            => _targets ?? ImmutableList<ProjectionTargetDescriptor>.Empty;

        /// <summary>
        /// Gets the projection revision.
        /// </summary>
        public long ProjectionRevision { get; }
    }
}
