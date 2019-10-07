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

using System.Linq;
using System.Reflection;
using AI4E.Messaging.Validation;

namespace AI4E.Messaging.MessageHandlers
{
    /// <summary>
    /// Contains extensions for the <see cref="MessageHandlerActionDescriptor"/> type.
    /// </summary>
    public static class ValidationMessageHandlerActionDescriptorExtension
    {
        /// <summary>
        /// Returns a boolean value indicating whether validation is enabled for the specified message handler.
        /// </summary>
        /// <param name="memberDescriptor">A <see cref="MessageHandlerActionDescriptor"/> that describes the message handler.</param>
        /// <returns>True if validation is enabled for <paramref name="memberDescriptor"/>, false otherwise.</returns>
        public static bool IsValidationEnabled(this MessageHandlerActionDescriptor memberDescriptor) // TODO: Add extra unit-tests for this, as this is public now?
        {
            var isValidationEnabled = memberDescriptor
                .MessageHandlerType
                .GetCustomAttribute<ValidateAttribute>(inherit: true)?.Validate ?? false;

            var attribute = memberDescriptor.Member.GetCustomAttribute<ValidateAttribute>(inherit: true);

            // If there is an attribute present in the inheritence hierarchy of the member but not on the
            // member itself, and there is a non-inherited attribute present on the type, use this.
            if (attribute != null &&
                memberDescriptor.Member.GetCustomAttribute<ValidateAttribute>(inherit: false) == null)
            {
                var nonInheritedTypeAttribute = memberDescriptor
                .MessageHandlerType
                .GetCustomAttribute<ValidateAttribute>(inherit: false);

                if (nonInheritedTypeAttribute != null)
                {
                    attribute = nonInheritedTypeAttribute;
                }
            }

            return attribute?.Validate ?? isValidationEnabled;
        }

        /// <summary>
        /// Attempts to retrieve the <see cref="ValidationDescriptor"/> for the specified message handler.
        /// </summary>
        /// <param name="memberDescriptor">A <see cref="MessageHandlerActionDescriptor"/> that describes the message handler.</param>
        /// <param name="validationDescriptor">Contains the <see cref="ValidationDescriptor"/> if the operation succeeds.</param>
        /// <returns>True if the operation succeeds, false otherwise.</returns>
        public static bool TryGetValidationDescriptor(  // TODO: Add extra unit-tests for this, as this is public now?
            this MessageHandlerActionDescriptor memberDescriptor,
            out ValidationDescriptor validationDescriptor)
        {
            if (!memberDescriptor.IsValidationEnabled())
            {
                validationDescriptor = default;
                return false;
            }

            var handledType = memberDescriptor.Member.GetParameters().First().ParameterType;

            do
            {
                if (ValidationDescriptor.TryGetDescriptor(
                    memberDescriptor.MessageHandlerType,
                    handledType,
                    out validationDescriptor))
                {
                    return true;
                }

                handledType = handledType.BaseType;
            }
            while (handledType != null);

            return false;
        }
    }
}
