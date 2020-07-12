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
    /// Represents a notification.
    /// </summary>
    public interface INotification
    {
        /// <summary>
        /// Gets a boolean value indicating whether the notification is expired.
        /// </summary>
        bool IsExpired { get; }

        /// <summary>
        /// Gets the type of notification.
        /// </summary>
        NotificationType NotificationType { get; }

        /// <summary>
        /// Gets the notification message.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Gets the notification description or <c>null</c> if the notification does not have a description.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets the URI of the notification target or <c>null</c> if no target is specified.
        /// </summary>
        public string? TargetUri { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the notification may be dismissed.
        /// </summary>
        bool AllowDismiss { get; }

        /// <summary>
        /// Gets the key of the notification or <c>null</c> if the notification does not have a key.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// Gets the timestamp (UTC) of the notification.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Dismisses the notification.
        /// </summary>
        void Dismiss();
    }
}
