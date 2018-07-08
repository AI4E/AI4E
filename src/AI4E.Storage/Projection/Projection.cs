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

namespace AI4E.Storage.Projection
{
    [Projection]
    public abstract class Projection { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class ProjectionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class NoProjectionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ProjectionMemberAttribute : Attribute
    {
        public ProjectionMemberAttribute() { }

        public Type ProjectionType { get; set; }

        public Type SourceType { get; set; }

        public bool? MultipleResults { get; set; }

        public bool ProjectNonExisting { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NoProjectionMemberAttribute : Attribute { }
}
