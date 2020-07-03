/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using System.Diagnostics.CodeAnalysis;

namespace AI4E.Storage.Domain
{
    internal static class EntityValidationHelper
    {
        public static void Validate([NotNull] Type? entityType, [NotNull] object? entity, bool validateEntityType = true)
        {
            if (validateEntityType)
            {
                Validate(entityType);
            }
            else if (entityType is null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException(Resources.EntityMustBeAssignableToEntityType, nameof(entity));

            Validate(entity);
        }

        public static void Validate([NotNull] object? entity)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            if (entity.GetType().IsDelegate())
                throw new ArgumentException(Resources.ArgumentMustNotBeADelegate, nameof(entity));

            if (entity.GetType().IsValueType)
                throw new ArgumentException(Resources.ArgumentMustNotBeAValueType, nameof(entity));
        }

        public static void Validate([NotNull] Type? entityType)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsDelegate())
                throw new ArgumentException(Resources.ArgumentMustNotSpecifyDelegateType, nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException(Resources.ArgumentMustNotSpecifyValueType, nameof(entityType));

            if (entityType.IsInterface)
                throw new ArgumentException(Resources.ArgumentMustNotSpecifyInterfaceType, nameof(entityType));

            if (entityType.IsGenericTypeDefinition)
                throw new ArgumentException(Resources.ArgumentMustNotSpecifyOpenTypeDefinition, nameof(entityType));
        }
    }
}
