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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Notifications.Test.Helpers;
using Xunit;

namespace AI4E.AspNetCore.Components.Notifications.Test
{
    public class NotificationManagerTests
    {
        [Fact]
        public void CtorNullDateTimeProviderThrowsArgumentNullExceptionTest()
        {
            // Arrange
            // -

            // Act
            static void Act()
            {
                new NotificationManager(dateTimeProvider: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("dateTimeProvider", Act);
        }

        [Fact]
        public void NewNotificationManagerHasNoNotificationsTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);

            // Act
            var notifications = subject.GetNotifications();

            // Assert
            Assert.Empty(notifications);
        }

        [Fact]
        public void PlaceNotificationNullNotificationMessageThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);

            // Act
            void Act()
            {
                subject.PlaceNotification(notificationMessage: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("notificationMessage", Act);
        }

        [Fact]
        public void PlaceNotificationOnDisposedObjectThrowsObjectDisposedExceptionTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            subject.Dispose();

            // Act
            void Act()
            {
                subject.PlaceNotification(notificationMessage);
            }

            // Assert
            Assert.Throws<ObjectDisposedException>(Act);
        }

        [Fact]
        public void PlaceNotificationPublishesNotificationTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();

            // Act
            subject.PlaceNotification(notificationMessage);

            // Assert
            Assert.Single(subject.GetNotifications());
        }

        [Fact]
        public void PlaceNotificationReturnsNotificationPlacementWithCorrectNotificationManagerTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();

            // Act
            var notificationPlacement = subject.PlaceNotification(notificationMessage);

            // Assert
            Assert.Same(subject, notificationPlacement.NotificationManager);
        }

        [Fact]
        public void PlaceNotificationWithNoneNotificationTypeIsNoOpTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage(NotificationType.None, string.Empty);

            // Act
            subject.PlaceNotification(notificationMessage);

            // Assert
            Assert.Empty(subject.GetNotifications());
        }

        [Fact]
        public void PlaceExpiredNotificationIsNoOpTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage
            {
                Expiration = dateTimeProvider.CurrentTime - TimeSpan.FromMinutes(1)
            };

            // Act
            subject.PlaceNotification(notificationMessage);

            // Assert
            Assert.Empty(subject.GetNotifications());
        }

        [Fact]
        public void PlaceNotificationRaisesNotificationsChangedEventTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            var raisedNotificationsChanged = false;

            void NotificationsChanged(object? sender, EventArgs e)
            {
                raisedNotificationsChanged = true;
            }

            subject.NotificationsChanged += NotificationsChanged;

            // Act
            subject.PlaceNotification(notificationMessage);

            // Assert
            Assert.True(raisedNotificationsChanged);
        }

        [Fact]
        public void CancelNotificationRemovesNotificationTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            var notificationPlacement = subject.PlaceNotification(notificationMessage);

            // Act
            subject.CancelNotification(notificationPlacement);

            // Assert
            Assert.Empty(subject.GetNotifications());
        }

        [Fact]
        public void CancelNotificationWithDefaultNotificationPlacementIsNoOpTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            subject.PlaceNotification(notificationMessage);

            // Act
            subject.CancelNotification(new NotificationPlacement());

            // Assert
            Assert.Single(subject.GetNotifications());
        }

        [Fact]
        public void CancelNotificationWithUnknownNotificationPlacementIsNoOpTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            subject.PlaceNotification(notificationMessage);

            // Act
            subject.CancelNotification(
                new NotificationPlacement(new NotificationManager(dateTimeProvider), new object()));

            // Assert
            Assert.Single(subject.GetNotifications());
        }

        [Fact]
        public void CancelNotificationRaisesNotificationsChangedEventTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            var notificationPlacement = subject.PlaceNotification(notificationMessage);

            var raisedNotificationsChanged = false;
            void NotificationsChanged(object? sender, EventArgs e)
            {
                raisedNotificationsChanged = true;
            }
            subject.NotificationsChanged += NotificationsChanged;

            // Act
            subject.CancelNotification(notificationPlacement);

            // Assert
            Assert.True(raisedNotificationsChanged);
        }

        [Fact]
        public async Task NotificationIsRemovedWhenExpiredTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage
            {
                Expiration = dateTimeProvider.CurrentTime + TimeSpan.FromMinutes(1)
            };
            subject.PlaceNotification(notificationMessage);

            // Act
            dateTimeProvider.CurrentTime += TimeSpan.FromMinutes(1);

            await Task.Delay(100);

            // Assert
            Assert.Empty(subject.GetNotifications());
        }

        [Fact]
        public async Task NotificationIsNotRemovedWhenDelayReturnsBeforeExpiredTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage
            {
                Expiration = dateTimeProvider.CurrentTime + TimeSpan.FromMinutes(1)
            };
            subject.PlaceNotification(notificationMessage);

            // Act
            dateTimeProvider.CompleteAllDelays();

            await Task.Delay(100);

            // Assert
            Assert.Single(subject.GetNotifications());
        }

        [Fact]
        public async Task NotificationExpirationLogicRetriesAfterDelayReturnsNotInTimeTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage
            {
                Expiration = dateTimeProvider.CurrentTime + TimeSpan.FromMinutes(1)
            };
            subject.PlaceNotification(notificationMessage);

            // Act
            dateTimeProvider.CompleteAllDelays();
            dateTimeProvider.CurrentTime += TimeSpan.FromMinutes(1);

            await Task.Delay(100);

            // Assert
            Assert.Empty(subject.GetNotifications());
        }

        [Fact]
        public async Task NotificationExpirationRaisesNotificationsChangedEventTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage
            {
                Expiration = dateTimeProvider.CurrentTime + TimeSpan.FromMinutes(1)
            };
            subject.PlaceNotification(notificationMessage);

            var raisedNotificationsChanged = false;
            void NotificationsChanged(object? sender, EventArgs e)
            {
                raisedNotificationsChanged = true;
            }
            subject.NotificationsChanged += NotificationsChanged;

            // Act
            dateTimeProvider.CurrentTime += TimeSpan.FromMinutes(1);

            await Task.Delay(100);

            // Assert
            Assert.True(raisedNotificationsChanged);
        }

        [Fact]
        public async Task NotificationExpirationDoesNotRaiseNotificationsChangedEventIfNotificationIsRemovedInTheMeantimeTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage
            {
                Expiration = dateTimeProvider.CurrentTime + TimeSpan.FromMinutes(1)
            };
            var notificationPlacement = subject.PlaceNotification(notificationMessage);
            subject.CancelNotification(notificationPlacement);

            var raisedNotificationsChanged = false;
            void NotificationsChanged(object? sender, EventArgs e)
            {
                raisedNotificationsChanged = true;
            }
            subject.NotificationsChanged += NotificationsChanged;

            // Act
            dateTimeProvider.CurrentTime += TimeSpan.FromMinutes(1);

            await Task.Delay(100);

            // Assert
            Assert.False(raisedNotificationsChanged);
        }

        [Fact]
        public void DismissNotificationRemovesNotificationTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            subject.PlaceNotification(notificationMessage);
            var notification = subject.GetNotifications().First();

            // Act
            subject.Dismiss(notification);

            // Assert
            Assert.Empty(subject.GetNotifications());
        }

        [Fact]
        public void DismissNotificationWithDefaultNotificationIsNoOpTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            subject.PlaceNotification(notificationMessage);

            // Act
            subject.Dismiss(new Notification());

            // Assert
            Assert.Single(subject.GetNotifications());
        }

        [Fact]
        public void DismissNotificationWithUnknownNotificationIsNoOpTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            subject.PlaceNotification(notificationMessage);

            // Act
            subject.Dismiss(
                new Notification(
                    new LinkedListNode<ManagedNotificationMessage>(
                        new ManagedNotificationMessage(
                            new NotificationMessage(), new NotificationManager(dateTimeProvider), dateTimeProvider))));

            // Assert
            Assert.Single(subject.GetNotifications());
        }

        [Fact]
        public void DismissNotificationRaisesNotificationsChangedEventTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);
            var notificationMessage = new NotificationMessage();
            subject.PlaceNotification(notificationMessage);
            var notification = subject.GetNotifications().First();

            var raisedNotificationsChanged = false;
            void NotificationsChanged(object? sender, EventArgs e)
            {
                raisedNotificationsChanged = true;
            }
            subject.NotificationsChanged += NotificationsChanged;

            // Act
            subject.Dismiss(notification);

            // Assert
            Assert.True(raisedNotificationsChanged);
        }

        [Fact]
        public void GetNotificationsByKeyTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);

            var notificationMessage1 = new NotificationMessage { Key = "a" };
            var notificationMessage2 = new NotificationMessage { Key = "a" };
            var notificationMessage3 = new NotificationMessage { Key = "b" };
            var notificationMessage4 = new NotificationMessage { Key = null };

            subject.PlaceNotification(notificationMessage1);
            subject.PlaceNotification(notificationMessage2);
            subject.PlaceNotification(notificationMessage3);
            subject.PlaceNotification(notificationMessage4);

            // Act
            var notifications = subject.GetNotifications(key: "a");

            // Assert
            static void AssertNotificationKey(Notification notification)
            {
                Assert.Equal("a", notification.Key);
            }

            Assert.Collection(notifications, AssertNotificationKey, AssertNotificationKey);
        }

        [Fact]
        public void GetNotificationByUriFilterTest()
        {
            // Arrange
            var dateTimeProvider = new TestDateTimeProvider();
            var subject = new NotificationManager(dateTimeProvider);

            var notificationMessage1 = new NotificationMessage { Key = "a", UriFilter = new UriFilter("http://example.domain") };
            var notificationMessage2 = new NotificationMessage { Key = "a", UriFilter = new UriFilter("http://example.domain/abc", exactMatch: true) };
            var notificationMessage3 = new NotificationMessage { Key = "a", UriFilter = default };
            var notificationMessage4 = new NotificationMessage { Key = null, UriFilter = new UriFilter("http://other.domain") };

            subject.PlaceNotification(notificationMessage1);
            subject.PlaceNotification(notificationMessage2);
            subject.PlaceNotification(notificationMessage3);
            subject.PlaceNotification(notificationMessage4);

            // Act
            var notifications = subject.GetNotifications(key:null, uri: "http://example.domain/abc");

            // Assert
            static void AssertNotificationKey(Notification notification)
            {
                Assert.Equal("a", notification.Key);
            }

            Assert.Collection(notifications, AssertNotificationKey, AssertNotificationKey, AssertNotificationKey);
        }
    }
}
