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
    /// <summary>
    /// Represents a notification message.
    /// </summary>
    /// <remarks>
    /// Instances of this type can be cached, as the notification manager does not store these internally and frees all 
    /// reference when the <see cref="INotificationManager.PlaceNotification(NotificationMessage)"/> call returns.
    /// </remarks>
    public sealed class NotificationMessage
    {
        private NotificationType _notificationType;
        private string _message;

        /// <summary>
        /// Creates a new instance of the <see cref="NotificationMessage"/> type.
        /// </summary>
        /// <param name="notificationType">The type of notification.</param>
        /// <param name="message">The notification message.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/>is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="notificationType"/> is an invalid value.
        /// </exception>
        public NotificationMessage(
            NotificationType notificationType,
            string message)
        {
            _notificationType = CheckValidNotificationType(notificationType, nameof(notificationType));
            _message = CheckValidMessage(message, nameof(message));
        }

        /// <summary>
        /// Creates a new instance of the <see cref="NotificationMessage"/> type.
        /// </summary>
        public NotificationMessage()
        {
            _notificationType = NotificationType.Info;
            _message = string.Empty;
        }

        /// <summary>
        /// Gets the type of notification.
        /// </summary>
        public NotificationType NotificationType
        {
            get => _notificationType;
            set => _notificationType = CheckValidNotificationType(value, nameof(value));
        }

        /// <summary>
        /// Gets the notification message.
        /// </summary>
        public string Message
        {
            get => _message;
            set => _message = CheckValidMessage(value, nameof(value));
        }

        /// <summary>
        /// Gets or sets the notification description or <c>null</c> if the notification does not have a description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the URI of the notification target or <c>null</c> if no target is specified.
        /// </summary>
        public string? TargetUri { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the notification's expiration (UTC)
        /// or <c>null</c> if the notification has no expiration.
        /// </summary>
        public DateTime? Expiration { get; set; }

        /// <summary>
        /// Gets or sets a boolean value indicating whether the notification may be dismissed.
        /// </summary>
        public bool AllowDismiss { get; set; }

        /// <summary>
        /// Gets or sets an URI filter that specifies on which pages the notification shall be displayed
        /// or <c>null</c> if it shall be displayed on all pages.
        /// </summary>
        public UriFilter UriFilter { get; set; }

        /// <summary>
        /// Gets or sets the key of the notification or <c>null</c> if the notification does not have a key.
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// Gets or sets the timestamp (UTC) of the notification or <c>null</c> to use the current time.
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Gets or sets a boolean value indicating whether a popup for the notification shall be displayed.
        /// </summary>
        public bool ShowPopup { get; set; } = true;

        private static NotificationType CheckValidNotificationType(
            NotificationType notificationType,
            string paramName)
        {
            if (!notificationType.IsValid())
            {
                throw new ArgumentException(
                    $"The argument must be one of the values defined in {typeof(NotificationType)}",
                    paramName);
            }

            return notificationType;
        }

        private static string CheckValidMessage(
            string message,
            string paramName)
        {
            if (message is null)
                throw new ArgumentNullException(paramName);

            return message;
        }
    }
}
