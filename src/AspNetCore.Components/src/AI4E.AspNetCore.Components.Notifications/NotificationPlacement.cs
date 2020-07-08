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

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <summary>
    /// A handle for a placement of a notification.
    /// </summary>
    public readonly struct NotificationPlacement : IEquatable<NotificationPlacement>, IDisposable
    {
        private static readonly object _noNotificationRef = new object();

        private readonly INotificationManager? _notificationManager;
        private readonly object? _notificationRef;

        /// <summary>
        /// Creates a new instance of the <see cref="NotificationPlacement"/> type.
        /// </summary>
        /// <param name="notificationManager">The notification manager the notification is placed at.</param>
        /// <param name="notificationRef">An opaque object reference that is implementation specific.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="notificationManager"/> or <paramref name="notificationRef"/> is 
        /// <c>null</c>.
        /// </exception>
        public NotificationPlacement(INotificationManager notificationManager, object notificationRef)
        {
            if (notificationManager is null)
                throw new ArgumentNullException(nameof(notificationManager));

            if (notificationRef is null)
                throw new ArgumentNullException(nameof(notificationRef));

            _notificationManager = notificationManager;
            _notificationRef = notificationRef;
        }

        /// <summary>
        /// Gets the notification manager the notification is placed at.
        /// </summary>
        public INotificationManager NotificationManager => _notificationManager ?? NoNotificationManager.Instance;

        /// <summary>
        /// Gets the opaque object reference.
        /// </summary>
        public object NotificationRef => _notificationRef ?? _noNotificationRef;

        /// <inheritdoc/>
        public void Dispose()
        {
            NotificationManager?.CancelNotification(this);
        }

        /// <inheritdoc/>
        public bool Equals(NotificationPlacement other)
        {
            // It is not necessary to include the INotificationManager into comparison.
            // A notification-ref belongs to a single INotificationManager in its complete lifetime,
            // so it should be suffice to compare the nodes.

            return NotificationRef == other.NotificationRef;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is NotificationPlacement notificationPlacement && Equals(notificationPlacement);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            // It is not necessary to include the INotificationManager into comparison.
            // A notification-ref belongs to a single INotificationManager in its complete lifetime,
            // so it should be suffice to compare the nodes.

            return NotificationRef?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Returns a boolean value indicating whether two notification placements are equal.
        /// </summary>
        /// <param name="left">The first notification.</param>
        /// <param name="right">The second notification.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in NotificationPlacement left, in NotificationPlacement right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two notification placements are not equal.
        /// </summary>
        /// <param name="left">The first notification.</param>
        /// <param name="right">The second notification.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in NotificationPlacement left, in NotificationPlacement right)
        {
            return !left.Equals(right);
        }

        internal bool IsOfScopedNotificationManager(INotificationManager notificationManager)
        {
            var current = NotificationManager;

            while (current is INotificationManagerScope scope)
            {
                current = scope.NotificationManager;

                if (current == notificationManager)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
