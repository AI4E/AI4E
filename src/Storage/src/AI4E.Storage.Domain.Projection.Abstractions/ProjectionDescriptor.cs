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
using System.Linq;
using System.Reflection;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Describes a projection.
    /// </summary>
    public readonly struct ProjectionDescriptor
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionDescriptor"/> type.
        /// </summary>
        /// <param name="handlerType">The type that declared the projection.</param>
        /// <param name="entityType">The entity type.</param>
        /// <param name="targetType">The projection target type.</param>
        /// <param name="member">A <see cref="MethodInfo"/> that specifies the projection (method).</param>
        /// <param name="multipleResults">
        /// A boolean value indicating whether the projection projections to multiple targets.
        /// </param>
        /// <param name="projectNonExisting">
        /// A boolean value indicating whether the projection shall be invoked for non-existing entities.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="handlerType"/>, <paramref name="entityType"/>,
        /// <paramref name="targetType"/> or <paramref name="member"/> is <c>null</c>.
        /// </exception>
        public ProjectionDescriptor(
            Type handlerType,
            Type entityType,
            Type targetType,
            MethodInfo member,
            bool multipleResults,
            bool projectNonExisting)
        {
            if (handlerType is null)
                throw new ArgumentNullException(nameof(handlerType));

            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            if (member == null)
                throw new ArgumentNullException(nameof(member));

            if (!entityType.IsOrdinaryClass())
                throw new ArgumentException("The argument must specify an ordinary class.", nameof(entityType));

            if (!targetType.IsOrdinaryClass())
                throw new ArgumentException("The argument must specify an ordinary class.", nameof(targetType));

            if (!handlerType.IsOrdinaryClass())
                throw new ArgumentException("The argument must specify an ordinary class.", nameof(handlerType));

            if (member.IsGenericMethodDefinition || member.ContainsGenericParameters)
                throw new ArgumentException("The member must neither be an open method definition nor must it contain generic parameters.");

            var firstParameter = member.GetParameters().Select(p => p.ParameterType).FirstOrDefault();

            if (firstParameter == null)
                throw new ArgumentException("The member must not be parameterless", nameof(member));

            if (!firstParameter.IsAssignableFrom(entityType))
                throw new ArgumentException("The specified entity type must be assignable the type of the members first parameter.");

            if (!member.DeclaringType.IsAssignableFrom(handlerType))
                throw new ArgumentException("The specified handler type must be assignable to the type that declares the specified member.");

            // TODO: Do we also check whether any parameter/messageType/messageHandlerType is by ref or is a pointer, etc.

            HandlerType = handlerType;
            EntityType = entityType;
            TargetType = targetType;
            Member = member;
            MultipleResults = multipleResults;
            ProjectNonExisting = projectNonExisting;
        }

        /// <summary>
        /// Gets the type that declared the projection.
        /// </summary>
        public Type HandlerType { get; }

        /// <summary>
        /// Gets the entity type.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the projection target type.
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// Gets a <see cref="MethodInfo"/> that specifies the projection (method).
        /// </summary>
        public MethodInfo Member { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the projection projections to multiple targets.
        /// </summary>
        public bool MultipleResults { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the projection shall be invoked for non-existing entity.
        /// </summary>
        public bool ProjectNonExisting { get; }
    }
}
