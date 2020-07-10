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
using System.Diagnostics;
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Notifications
{
    internal readonly struct PopupNotification : IPopupNotification, IEquatable<PopupNotification>
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        internal PopupNotification(LinkedListNode<ManagedNotificationMessage> notificationRef, IDateTimeProvider dateTimeProvider)
        {
            Debug.Assert(notificationRef != null);
            NotificationRef = notificationRef;
            _dateTimeProvider = dateTimeProvider;
        }

        internal LinkedListNode<ManagedNotificationMessage>? NotificationRef { get; }

        public bool ShowPopup => NotificationRef?.Value.ShowPopup ?? false;

        public DateTime? Expiration => NotificationRef?.Value.Expiration;

        public Notification Notification => 
            NotificationRef is null ? 
            default : 
            new Notification(NotificationRef, _dateTimeProvider);

        public bool Equals(PopupNotification other)
        {
            return NotificationRef == other.NotificationRef;
        }

        public override bool Equals(object? obj)
        {
            return obj is PopupNotification notification && Equals(notification);
        }

        public override int GetHashCode()
        {
            return NotificationRef?.GetHashCode() ?? 0;
        }

        public static bool operator ==(PopupNotification left, PopupNotification right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PopupNotification left, PopupNotification right)
        {
            return !left.Equals(right);
        }
    }
}
