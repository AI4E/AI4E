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

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <inheritdoc cref="INotificationManagerScope"/>
    public sealed class NotificationManagerScope : INotificationManagerScope
    {
        private readonly HashSet<object> _notifications = new HashSet<object>();
        private bool _isDisposed = false;

        /// <summary>
        /// Creates a new instance of the <see cref="NotificationManagerScope"/> type.
        /// </summary>
        /// <param name="notificationManager">The underlying notification manager.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="notificationManager"/> is <c>null</c>.
        /// </exception>
        public NotificationManagerScope(INotificationManager notificationManager)
        {
            if (notificationManager is null)
                throw new ArgumentNullException(nameof(notificationManager));

            NotificationManager = notificationManager;
        }

        INotificationManagerScope INotificationManager.CreateScope()
        {
            return new NotificationManagerScope(this);
        }

        INotificationRecorder INotificationManager.CreateRecorder()
        {
            return new NotificationRecorder(this);
        }

        /// <inheritdoc/>
        public INotificationManager NotificationManager { get; }

        /// <inheritdoc/>
        public NotificationPlacement PlaceNotification(NotificationMessage notificationMessage)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (notificationMessage is null)
                throw new ArgumentNullException(nameof(notificationMessage));

            var placement = NotificationManager.PlaceNotification(notificationMessage);
            _notifications.Add(placement.NotificationRef);

            return new NotificationPlacement(NotificationManager, placement.NotificationRef);
        }

        /// <inheritdoc/>
        public void CancelNotification(in NotificationPlacement notificationPlacement)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (notificationPlacement.NotificationManager != this)
            {
                if (notificationPlacement.IsOfScopedNotificationManager(this))
                {
                    notificationPlacement.NotificationManager.CancelNotification(notificationPlacement);
                }
            }
            else if (_notifications.Remove(notificationPlacement.NotificationRef))
            {
                NotificationManager.CancelNotification(
                    new NotificationPlacement(NotificationManager, notificationPlacement.NotificationRef));
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            foreach (var notification in _notifications)
            {
                NotificationManager.CancelNotification(
                    new NotificationPlacement(NotificationManager, notification));
            }

            _notifications.Clear();
        }
    }
}
