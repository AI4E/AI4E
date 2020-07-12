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
using System.Collections.Generic;

namespace AI4E.AspNetCore.Components.Notifications
{
    public static class NotificationManagerExtension
    {
        /// <summary>
        /// Gets the collection of all available notifications.
        /// </summary>
        /// <param name="notificationManager">The notification manager.</param>
        /// <returns>An<see cref="IReadOnlyList{T}"/> of all available notifications.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="notificationManager"/> is <c>null</c>.
        /// </exception>
        public static IReadOnlyList<TNotification> GetNotifications<TNotification>(
            this INotificationManager<TNotification> notificationManager)
            where TNotification : INotification, IEquatable<TNotification>
        {
            if (notificationManager is null)
                throw new ArgumentNullException(nameof(notificationManager));

            return notificationManager.GetNotifications(key: null, uri: null);
        }

        /// <summary>
        /// Gets the collection of all available notifications in the context of the specified filters.
        /// </summary>
        /// <param name="notificationManager">The notification manager.</param>
        /// <param name="key"> The key a notification must match or <c>null</c> to disable filtering by key.</param>
        /// <returns>An<see cref="IReadOnlyList{T}"/> of all available notifications.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="notificationManager"/> is <c>null</c>.
        /// </exception>
        public static IReadOnlyList<TNotification> GetNotifications<TNotification>(
            this INotificationManager<TNotification> notificationManager,
            string? key) where TNotification : INotification, IEquatable<TNotification>
        {
            if (notificationManager is null)
                throw new ArgumentNullException(nameof(notificationManager));

            return notificationManager.GetNotifications(key, uri: null);
        }

        /// <summary>
        /// Gets the collection of all available notifications in the context of the specified filters.
        /// </summary>
        /// <param name="notificationManager">The notification manager.</param>
        /// <param name = "key"> The key a notification must match or <c>null</c> to disable filtering by key.</param>
        /// <param name = "uri">
        /// The URI a notification's URI filter must match or <c>null</c> to disable filtering by URI.
        /// </param>
        /// <returns>An<see cref="IReadOnlyList{T}"/> of all available notifications.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="notificationManager"/> is <c>null</c>.
        /// </exception>
        public static IReadOnlyList<TNotification> GetNotifications<TNotification>(
            this INotificationManager<TNotification> notificationManager,
            string? key,
#pragma warning disable CA1054 
            string? uri) where TNotification : INotification, IEquatable<TNotification>
#pragma warning restore CA1054
        {
            if (notificationManager is null)
                throw new ArgumentNullException(nameof(notificationManager));

            return notificationManager.GetNotifications(key, uri is null ? null : new Uri(uri));
        }
    }
}
