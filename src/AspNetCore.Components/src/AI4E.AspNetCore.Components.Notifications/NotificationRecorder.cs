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
using System.Collections.Generic;

namespace AI4E.AspNetCore.Components.Notifications
{
    public sealed class NotificationRecorder : INotificationRecorder
    {
        private readonly HashSet<RecordedNotification> _notifications = new HashSet<RecordedNotification>();
        private bool _isDisposed = false;

        public NotificationRecorder(INotificationManager notificationManager)
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

        public INotificationManager NotificationManager { get; }

        public NotificationPlacement PlaceNotification(NotificationMessage notificationMessage)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            var recordedNotification = new RecordedNotification(notificationMessage);
            _notifications.Add(recordedNotification);
            return new NotificationPlacement(this, recordedNotification);
        }

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
            else if (notificationPlacement.NotificationRef is RecordedNotification recordedNotification
                && _notifications.Remove(recordedNotification)
                && recordedNotification.NotificationRef != null)
            {
                NotificationManager.CancelNotification(
                    new NotificationPlacement(NotificationManager, recordedNotification.NotificationRef));
            }
        }

        public void PublishNotifications()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            foreach (var notification in _notifications)
            {
                if (notification.NotificationRef != null)
                    continue;

                var placement = NotificationManager.PlaceNotification(notification.NotificationMessage);
                notification.Publish(placement.NotificationRef);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            foreach (var notification in _notifications)
            {
                if (notification.NotificationRef is null)
                    continue;

                NotificationManager.CancelNotification(
                    new NotificationPlacement(NotificationManager, notification.NotificationRef));
            }

            _notifications.Clear();
        }

        private sealed class RecordedNotification
        {
            public RecordedNotification(NotificationMessage notificationMessage)
            {
                NotificationMessage = notificationMessage;
            }

            public NotificationMessage NotificationMessage { get; }
            public object? NotificationRef { get; private set; }

            public void Publish(object notificationRef)
            {
                if (notificationRef is null)
                    throw new ArgumentNullException(nameof(notificationRef));

                NotificationRef = notificationRef;
            }
        }
    }
}
