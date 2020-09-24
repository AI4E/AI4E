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

using System.Linq;
using AI4E.Utils;
using Microsoft.Extensions.Options;
using Xunit;

namespace AI4E.AspNetCore.Components.Notifications.Test
{
    public sealed class Issue317
    {
        [Fact]
        public void Issue317Test()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProvider();
            var optionsProvider = Options.Create(new NotificationOptions());
            var notificationManager = new NotificationManager(dateTimeProvider, optionsProvider);

            var notificationRecorder = notificationManager.CreateRecorder();
            notificationRecorder.PlaceNotification(new NotificationMessage { AllowDismiss = true });
            notificationRecorder.PublishNotifications();

            var notification = notificationManager.GetNotifications().First();
            notification.Dismiss();

            // Act
            notificationRecorder.Dispose();

            // Assert
            Assert.Empty(notificationManager.GetNotifications());
        }
    }
}
