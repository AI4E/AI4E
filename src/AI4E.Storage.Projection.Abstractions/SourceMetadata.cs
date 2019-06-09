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

namespace AI4E.Storage.Projection
{
    public readonly struct SourceMetadata
    {
        public SourceMetadata(
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

            Dependencies = (dependencies as ImmutableList<ProjectionSourceDependency>) ?? dependencies.ToImmutableList();
            Targets = (targets as ImmutableList<ProjectionTargetDescriptor>) ?? targets.ToImmutableList();
            ProjectionRevision = projectionRevision;
        }

        public IReadOnlyCollection<ProjectionSourceDependency> Dependencies { get; }
        public IReadOnlyCollection<ProjectionTargetDescriptor> Targets { get; }
        public long ProjectionRevision { get; }
    }
}
