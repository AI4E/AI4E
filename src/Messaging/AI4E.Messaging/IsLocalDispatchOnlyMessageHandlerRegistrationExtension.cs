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

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extension methods for the <see cref="IMessageHandlerRegistration"/> type.
    /// </summary>
    public static class IsLocalDispatchOnlyMessageHandlerRegistrationExtension
    {
        /// <summary>
        /// Gets a boolean value indicating whether the 'local dispatch only' feature is enabled.
        /// </summary>
        /// <param name="handlerRegistration">The message handler registration.</param>
        /// <returns>
        /// True if the 'local dispatch only' feature is enabled for <paramref name="handlerRegistration"/>,
        /// false otherwise.
        /// </returns>
        public static bool IsLocalDispatchOnly(this IMessageHandlerRegistration handlerRegistration)
        {
            return handlerRegistration.Configuration.IsEnabled<LocalDispatchOnlyMessageHandlerConfiguration>();
        }
    }
}
