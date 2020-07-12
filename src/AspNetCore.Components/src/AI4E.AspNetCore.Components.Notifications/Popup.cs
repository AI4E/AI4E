/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c)  2020 Andreas Truetschel and contributors.
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
    public readonly struct Popup : INotification, IEquatable<Popup>
    {
        private readonly IManagedPopup? _managedPopup;

        internal Popup(IManagedPopup managedPopup)
        {
            _managedPopup = managedPopup;
        }

        /// <inheritdoc />
        public bool Equals(Popup other)
        {
            return _managedPopup == other._managedPopup;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is Popup popup && Equals(popup);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _managedPopup?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Returns a boolean value indicating whether two notifications are equal.
        /// </summary>
        /// <param name="left">The first notification.</param>
        /// <param name="right">The second notification.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(Popup left, Popup right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two notifications are not equal.
        /// </summary>
        /// <param name="left">The first notification.</param>
        /// <param name="right">The second notification.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(Popup left, Popup right)
        {
            return !left.Equals(right);
        }

        private Notification Notification => _managedPopup?.Notification ?? default;

        /// <inheritdoc />
        public bool IsExpired => Notification.IsExpired;

        /// <inheritdoc />
        public NotificationType NotificationType => Notification.NotificationType;

        /// <inheritdoc />
        public string Message => Notification.Message;

        /// <inheritdoc />
        public string? Description => Notification.Description;

        /// <inheritdoc />
        public string? TargetUri => Notification.TargetUri;

        /// <inheritdoc />
        bool INotification.AllowDismiss => Notification.AllowDismiss;

        /// <inheritdoc />
        public string? Key => Notification.Key;

        /// <inheritdoc />
        public DateTime Timestamp => Notification.Timestamp;

        /// <summary>
        /// Gets the date and time of the popup's expiration (UTC).
        /// </summary>
        public DateTime PopupExpiration => _managedPopup?.PopupExpiration ?? DateTime.UtcNow;

        void INotification.Dismiss()
        {
            Notification.Dismiss();
        }

        /// <inheritdoc />
        public void DismissOrHide()
        {
            // The notification is either already expired or cannot be dismissed.
            // The notification can never go back to state "non-expired".

            
            var notification = Notification;
            
            if (notification.IsExpired)
                return;

            if (notification.AllowDismiss)
            {
                notification.Dismiss();
            }
            else
            {
                // Cancel the popup
                _managedPopup?.Cancel();
            }
        }
    }
}
