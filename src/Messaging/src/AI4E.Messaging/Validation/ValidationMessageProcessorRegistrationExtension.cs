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

using System.Reflection;
using AI4E.Messaging.Validation;

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extension methods for the <see cref="IMessageProcessorRegistration"/> type.
    /// </summary>
    public static class ValidationMessageProcessorRegistrationExtension
    {
        /// <summary>
        /// Returns a boolean value indicating whether the message processor shall be called on validation dispatches.
        /// </summary>
        /// <param name="messageProcessorRegistration">The message processor registration.</param>
        /// <returns>
        /// True if the registrered message processor shall be called on validation dispatched, false otherwise.
        /// </returns>
        public static bool CallOnValidation(this IMessageProcessorRegistration messageProcessorRegistration)
        {
            var processorType = messageProcessorRegistration.MessageProcessorType;

            var callOnValidationAttribute = processorType
                .GetCustomAttribute<CallOnValidationAttribute>(inherit: true);

            return callOnValidationAttribute != null && callOnValidationAttribute.CallOnValidation;
        }
    }
}
