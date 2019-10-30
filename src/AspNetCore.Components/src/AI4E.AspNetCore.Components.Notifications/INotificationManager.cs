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
    public interface INotificationManager : IDisposable // TODO: Rename?
    {
        NotificationPlacement PlaceNotification(NotificationMessage notificationMessage);
        void CancelNotification(in NotificationPlacement notificationPlacement);

        INotificationManagerScope CreateScope();
        INotificationRecorder CreateRecorder();
    }

    public interface INotificationManager<TNotification> : INotificationManager
        where TNotification : INotification, IEquatable<TNotification>
    {
        event EventHandler NotificationsChanged;

        IEnumerable<TNotification> GetNotifications();
        void Dismiss(TNotification notification);
        IEnumerable<TNotification> GetNotifications(string key);
        IEnumerable<TNotification> GetNotifications(string key, string uri);
        IEnumerable<TNotification> GetNotifications(string key, Uri uri);
    }
}
