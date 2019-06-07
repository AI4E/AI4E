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

namespace AI4E.Storage.Projection
{
    public readonly struct ProjectionTargetDescriptor : IEquatable<ProjectionTargetDescriptor>
    {
        public ProjectionTargetDescriptor(Type targetType, string targetId)
        {
            if (targetType is null)
                throw new ArgumentNullException(nameof(targetType));

            if (targetId is null || targetId.Equals(default))
                throw new ArgumentDefaultException(nameof(targetId));

            TargetType = targetType;
            TargetId = targetId;
        }

        public Type TargetType { get; }
        public string TargetId { get; }

        public override bool Equals(object obj)
        {
            return obj is ProjectionTargetDescriptor entityDescriptor && Equals(entityDescriptor);
        }

        public bool Equals(ProjectionTargetDescriptor other)
        {
            if (other.TargetType is null && TargetType is null)
                return true;

            return (other.TargetType, other.TargetId) == (TargetType, TargetId);
        }

        public override int GetHashCode()
        {
            if (TargetType is null)
                return 0;

            return (TargetType, TargetId).GetHashCode();
        }

        public static bool operator ==(in ProjectionTargetDescriptor left, in ProjectionTargetDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ProjectionTargetDescriptor left, in ProjectionTargetDescriptor right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct ProjectionTargetDescriptor<TId> : IEquatable<ProjectionTargetDescriptor<TId>>
    {
        public ProjectionTargetDescriptor(Type targetType, TId targetId)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (targetId == null || targetId.Equals(default))
                throw new ArgumentDefaultException(nameof(targetId));

            TargetType = targetType;
            TargetId = targetId;
        }

        public Type TargetType { get; }
        public TId TargetId { get; }
        public string StringifiedTargetId => TargetId.ToString();

        public override bool Equals(object obj)
        {
            return obj is ProjectionTargetDescriptor<TId> entityDescriptor && Equals(entityDescriptor);
        }

        public bool Equals(ProjectionTargetDescriptor<TId> other)
        {
            if (other.TargetType is null && TargetType is null)
                return true;

            return other.TargetType == TargetType && EqualityComparer<TId>.Default.Equals(other.TargetId, TargetId);
        }

        public override int GetHashCode()
        {
            if (TargetType == null)
                return 0;

            return (TargetType, TargetId).GetHashCode();
        }

        public static bool operator ==(in ProjectionTargetDescriptor<TId> left, in ProjectionTargetDescriptor<TId> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ProjectionTargetDescriptor<TId> left, in ProjectionTargetDescriptor<TId> right)
        {
            return !left.Equals(right);
        }

        public static implicit operator ProjectionTargetDescriptor(in ProjectionTargetDescriptor<TId> typedDescriptor)
        {
            return new ProjectionTargetDescriptor(typedDescriptor.TargetType, typedDescriptor.StringifiedTargetId);
        }
    }
}
