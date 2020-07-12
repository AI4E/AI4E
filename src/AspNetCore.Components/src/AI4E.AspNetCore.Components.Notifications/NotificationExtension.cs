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

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <summary>
    /// Contains extensions for the <see cref="INotification"/> type.
    /// </summary>
    public static class NotificationExtension
    {
        /// <summary>
        /// Formats the title of a notification.
        /// </summary>
        /// <typeparam name="TNotification">The type of notification.</typeparam>
        /// <param name="notification">The notification.</param>
        /// <returns>The formatted notification title.</returns>
        /// <exception cref="ArgumentNullException">
        /// Throws if <paramref name="notification"/> is <c>null</c>.
        /// </exception>
        public static string FormatTitle<TNotification>(this TNotification notification)
            where TNotification : INotification
        {
            if (notification is null)
                throw new ArgumentNullException(nameof(notification));

            if (!string.IsNullOrWhiteSpace(notification.Message))
            {
                return notification.Message;
            }

            return notification.NotificationType.ToString();
        }
    }
}
