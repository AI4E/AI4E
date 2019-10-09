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
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <summary>
    /// Represents a notification message under the control of a notification manager.
    /// </summary>
    internal sealed class ManagedNotificationMessage
    {
        public ManagedNotificationMessage(
            NotificationMessage notificationMessage,
            NotificationManager notificationManager,
            IDateTimeProvider dateTimeProvider)
        {
            if (notificationMessage is null)
                throw new ArgumentNullException(nameof(notificationMessage));

            if (notificationManager is null)
                throw new ArgumentNullException(nameof(notificationManager));

            if (dateTimeProvider is null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            NotificationManager = notificationManager!;

            NotificationType = notificationMessage.NotificationType;
            Message = notificationMessage.Message;
            Description = notificationMessage.Description;
            TargetUri = notificationMessage.TargetUri;
            Expiration = notificationMessage.Expiration;
            AllowDismiss = notificationMessage.AllowDismiss;
            UriFilter = notificationMessage.UriFilter;
            Key = notificationMessage.Key;
            Timestamp = notificationMessage.Timestamp ?? dateTimeProvider.GetCurrentTime();
        }

        public NotificationManager NotificationManager { get; }

        /// <summary>
        /// Gets the type of notification.
        /// </summary>
        public NotificationType NotificationType { get; }

        /// <summary>
        /// Gets the notification message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the notification description.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets the uri of the notification target.
        /// </summary>
        public string? TargetUri { get; }

        /// <summary>
        /// Gets the date and time of the notification's expiration
        /// or <c>null</c> if the notification has no expiration.
        /// </summary>
        public DateTime? Expiration { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the notification may be dismissed.
        /// </summary>
        public bool AllowDismiss { get; }

        /// <summary>
        /// Gets an url filter that specifies on which pages the alert shall be displayed
        /// or <c>null</c> if it shall be displayed on all pages.
        /// </summary>
        public UriFilter UriFilter { get; }

        /// <summary>
        /// Gets the notification key.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// Gets the timestamp of the notification.
        /// </summary>
        public DateTime Timestamp { get; }
    }
}
