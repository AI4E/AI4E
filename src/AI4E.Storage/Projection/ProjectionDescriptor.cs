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

using System;
using System.Reflection;

namespace AI4E.Storage.Projection
{
    public class ProjectionDescriptor
    {
        public ProjectionDescriptor(Type sourceType, Type projectionType, bool multipleResults, MethodInfo member)
        {
            if (sourceType == null)
                throw new ArgumentNullException(nameof(sourceType));

            if (projectionType == null)
                throw new ArgumentNullException(nameof(projectionType));

            if (member == null)
                throw new ArgumentNullException(nameof(member));

            SourceType = sourceType;
            ProjectionType = projectionType;
            MultipleResults = multipleResults;
            Member = member;
        }

        public Type SourceType { get; }
        public Type ProjectionType { get; }
        public bool MultipleResults { get; }
        public MethodInfo Member { get; }
    }
}
