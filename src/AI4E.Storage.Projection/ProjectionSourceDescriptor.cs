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
    internal readonly struct ProjectionSourceDescriptor : IEquatable<ProjectionSourceDescriptor>
    {
        public ProjectionSourceDescriptor(Type sourceType, string sourceId)
        {
            if (sourceType == null)
                throw new ArgumentNullException(nameof(sourceType));

            if (sourceId == null || sourceId.Equals(default))
                throw new ArgumentDefaultException(nameof(sourceId));

            SourceType = sourceType;
            SourceId = sourceId;
        }

        public Type SourceType { get; }
        public string SourceId { get; }

        public override bool Equals(object obj)
        {
            return obj is ProjectionSourceDescriptor entityDescriptor && Equals(entityDescriptor);
        }

        public bool Equals(ProjectionSourceDescriptor other)
        {
            return other.SourceType == null && SourceType == null || other.SourceType == SourceType && other.SourceId.Equals(SourceId);
        }

        public override int GetHashCode()
        {
            if (SourceType == null)
                return 0;

            return SourceType.GetHashCode() ^ SourceId.GetHashCode();
        }

        public static bool operator ==(in ProjectionSourceDescriptor left, in ProjectionSourceDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ProjectionSourceDescriptor left, in ProjectionSourceDescriptor right)
        {
            return !left.Equals(right);
        }
    }
}
