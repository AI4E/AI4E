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

using AI4E.Messaging.Validation;

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains validation specific extension for the <see cref="IMessagingBuilder"/> type.
    /// </summary>
    public static class ValidationMessagingBuilderExtension
    {
        /// <summary>
        /// Adds validation to the messaging system.
        /// </summary>
        /// <param name="builder">The messaging builder.</param>
        /// <returns>The messaging builder.</returns>
        public static IMessagingBuilder UseValidation(this IMessagingBuilder builder)
        {
            ValidationMessageProcessor.Register(builder);
            ValidationMessageHandler.Register(builder);
            ValidationRouteResolver.Register(builder);
            return builder;
        }
    }
}
