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
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <inheritdoc cref="INotification"/>
    public readonly struct Notification : INotification, IEquatable<Notification>
    {
        private readonly IDateTimeProvider? _dateTimeProvider;

        internal Notification(LinkedListNode<ManagedNotificationMessage> notificationRef, IDateTimeProvider dateTimeProvider)
        {
            NotificationRef = notificationRef;
            _dateTimeProvider = dateTimeProvider;
        }

        internal LinkedListNode<ManagedNotificationMessage>? NotificationRef { get; }

        /// <summary>
        /// Gets the notification manager that manages the notification
        /// or <c>null</c> if the notification is a default value.
        /// </summary>
        public INotificationManager<Notification>? NotificationManager =>
            NotificationRef?.Value.NotificationManager;

        /// <inheritdoc />
        public bool Equals(Notification other)
        {
            return NotificationRef == other.NotificationRef;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is Notification notification && Equals(notification);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return NotificationRef?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Returns a boolean value indicating whether two notifications are equal.
        /// </summary>
        /// <param name="left">The first notification.</param>
        /// <param name="right">The second notification.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(Notification left, Notification right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two notifications are not equal.
        /// </summary>
        /// <param name="left">The first notification.</param>
        /// <param name="right">The second notification.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(Notification left, Notification right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public bool IsExpired
        {
            get
            {
                if (NotificationRef is null || _dateTimeProvider is null)
                    return true;

                var expiration = NotificationRef.Value.Expiration;

                if (expiration is null)
                    return false;

                var now = _dateTimeProvider.GetCurrentTime();
                return now >= expiration;
            }
        }

        /// <inheritdoc />
        public NotificationType NotificationType => NotificationRef?.Value.NotificationType ?? NotificationType.None;

        /// <inheritdoc />
        public string Message => NotificationRef?.Value.Message ?? string.Empty;

        /// <inheritdoc />
        public string? Description => NotificationRef?.Value.Description;

        /// <inheritdoc />
        public string? TargetUri => NotificationRef?.Value.TargetUri;

        /// <inheritdoc />
        public bool AllowDismiss => !IsExpired && (NotificationRef?.Value.AllowDismiss ?? false);

        /// <inheritdoc />
        public string? Key => NotificationRef?.Value.Key;

        /// <inheritdoc />
        public DateTime Timestamp => NotificationRef?.Value.Timestamp ?? DateTime.UtcNow; // TODO: Which value can we use as timestamp here?

        /// <inheritdoc />
        public void Dismiss()
        {
            // The notification is either already expired or cannot be dismissed.
            // The notification can never go back to state "non-expired".
            if (!AllowDismiss)
                return;

            NotificationManager?.Dismiss(this);
        }
    }
}
