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

namespace AI4E.AspNetCore.Components.Notifications
{
    public readonly struct NotificationPlacement : IEquatable<NotificationPlacement>, IDisposable
    {
        public NotificationPlacement(INotificationManager notificationManager, object notificationRef)
        {
            if (notificationManager is null)
                throw new ArgumentNullException(nameof(notificationManager));

            if (notificationRef is null)
                throw new ArgumentNullException(nameof(notificationRef));

            NotificationManager = notificationManager;
            NotificationRef = notificationRef;
        }

        public INotificationManager NotificationManager { get; }
        public object NotificationRef { get; }

        public void Dispose()
        {
            NotificationManager?.CancelNotification(this);
        }

        public bool Equals(NotificationPlacement other)
        {
            // It is not necessary to include the INotificationManager into comparison.
            // A notification-ref belongs to a single INotificationManager in its complete lifetime,
            // so it should be suffice to compare the nodes.

            return NotificationRef == other.NotificationRef;
        }

#if NETSTD20
        public override bool Equals(object obj)
#else
        public override bool Equals(object? obj)
#endif
        {
            return obj is NotificationPlacement notificationPlacement && Equals(notificationPlacement);
        }

        public override int GetHashCode()
        {
            // It is not necessary to include the INotificationManager into comparison.
            // A notification-ref belongs to a single INotificationManager in its complete lifetime,
            // so it should be suffice to compare the nodes.

            return NotificationRef?.GetHashCode() ?? 0;
        }

        public static bool operator ==(in NotificationPlacement left, in NotificationPlacement right)
        {
            return left.Equals(right);
        }

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
