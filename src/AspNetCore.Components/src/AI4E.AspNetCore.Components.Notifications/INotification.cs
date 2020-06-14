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
        /// Gets the notification description.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets the uri of the notification target.
        /// </summary>
        public string? TargetUri { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the notification may be dismissed.
        /// </summary>
        bool AllowDismiss { get; }

        /// <summary>
        /// Gets the notification key.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// Gets the timestamp of the notification.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Dismisses the notification.
        /// </summary>
        void Dismiss();
    }
}
