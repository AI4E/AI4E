/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
    /// <summary>
    /// Represents a notification manager that manages notifications and their lifetime.
    /// </summary>
    public interface INotificationManager : IDisposable
    {
        /// <summary>
        /// Places the specified notification message and returns a handle for the notification placement.
        /// </summary>
        /// <param name="notificationMessage">The notification message describing the notification to place.</param>
        /// <returns>
        /// A <see cref="NotificationPlacement"/> that is a handle for the placement of the notification.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Throw if <paramref name="notificationMessage"/> is <c>null</c>.
        /// </exception>
        NotificationPlacement PlaceNotification(NotificationMessage notificationMessage);

        /// <summary>
        /// Cancels the specified notifications placement.
        /// </summary>
        /// <param name="notificationPlacement">The notification placement to cancel.</param>
        void CancelNotification(in NotificationPlacement notificationPlacement);

        /// <summary>
        /// Creates a notification manager scope for the current instance.
        /// </summary>
        /// <returns>A new <see cref="INotificationManagerScope"/> based on the current notification manager.</returns>
        INotificationManagerScope CreateScope();

        /// <summary>
        /// Creates a new notification recorder for the current instance.
        /// </summary>
        /// <returns>A new <see cref="INotificationRecorder"/> based on the current notification manager.</returns>
        INotificationRecorder CreateRecorder();
    }

    /// <summary>
    /// Represents a notification manager of the specified type of notification that manages notifications and their 
    /// lifetime.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    public interface INotificationManager<TNotification> : INotificationManager
        where TNotification : INotification, IEquatable<TNotification>
    {
        /// <summary>
        /// Raised when the notifications managed by the current notification manager change.
        /// </summary>
        event EventHandler NotificationsChanged;

        /// <summary>
        /// Gets the collection of all available notifications.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> of all available notifications.</returns>
        IEnumerable<TNotification> GetNotifications();

        // TODO: Throw away all these and replace with a common method
        IEnumerable<TNotification> GetNotifications(string key);
        IEnumerable<TNotification> GetNotifications(string key, string uri);
        IEnumerable<TNotification> GetNotifications(string key, Uri uri);

        /// <summary>
        /// Dismisses the specified notification.
        /// </summary>
        /// <param name="notification">The notification to dismiss.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="notification"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// This is a no-op if <paramref name="notification"/> is not dismissable or is not known to the current 
        /// notification manager.
        /// </remarks>
        void Dismiss(TNotification notification);
    }
}
