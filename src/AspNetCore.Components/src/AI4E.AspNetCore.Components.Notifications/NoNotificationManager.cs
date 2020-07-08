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

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <summary>
    /// A null-object for the <see cref="INotificationManager"/> type.
    /// </summary>
    public sealed class NoNotificationManager : INotificationManager
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="NoNotificationManager"/> type.
        /// </summary>
        public static NoNotificationManager Instance { get; } = new NoNotificationManager();

        private NoNotificationManager() { }

        /// <inheritdoc/>
        public NotificationPlacement PlaceNotification(NotificationMessage notificationMessage)
        {
            return default;
        }

        /// <inheritdoc/>
        public void CancelNotification(in NotificationPlacement notificationPlacement) { }

        /// <inheritdoc/>
        public INotificationManagerScope CreateScope()
        {
            // TODO: Return a null-object instead?
            return new NotificationManagerScope(this);
        }

        /// <inheritdoc/>
        public INotificationRecorder CreateRecorder()
        {
            // TODO: Return a null-object instead?
            return new NotificationRecorder(this);
        }

        /// <inheritdoc/>
        public void Dispose() { }
    }
}
