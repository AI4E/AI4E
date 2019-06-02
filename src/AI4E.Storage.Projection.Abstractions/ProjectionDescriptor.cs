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
using System.Reflection;

namespace AI4E.Storage.Projection
{
    public readonly struct ProjectionDescriptor
    {
        public ProjectionDescriptor(
            Type handlerType,
            Type sourceType,
            Type targetType,
            bool multipleResults,
            bool projectNonExisting,
            MethodInfo member)
        {
            if (handlerType is null)
                throw new ArgumentNullException(nameof(handlerType));

            if (sourceType == null)
                throw new ArgumentNullException(nameof(sourceType));

            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (member == null)
                throw new ArgumentNullException(nameof(member));

            // TODO: Add validation.

            HandlerType = handlerType;
            SourceType = sourceType;
            TargetType = targetType;
            MultipleResults = multipleResults;
            ProjectNonExisting = projectNonExisting;
            Member = member;
        }

        public Type HandlerType { get; }
        public Type SourceType { get; }
        public Type TargetType { get; }
        public bool MultipleResults { get; }
        public bool ProjectNonExisting { get; }
        public MethodInfo Member { get; }
    }
}
