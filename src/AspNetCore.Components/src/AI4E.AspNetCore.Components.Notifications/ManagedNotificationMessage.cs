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
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Notifications
{
    // Represents a notification message under the control of a notification manager.
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
        public NotificationType NotificationType { get; }
        public string Message { get; }
        public string? Description { get; }
        public string? TargetUri { get; }
        public DateTime? Expiration { get; }
        public bool AllowDismiss { get; }
        public UriFilter UriFilter { get; }
        public string? Key { get; }
        public DateTime Timestamp { get; }
    }
}
