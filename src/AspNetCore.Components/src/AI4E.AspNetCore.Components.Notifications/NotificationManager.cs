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
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Utils;
using Microsoft.Extensions.Options;

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <inheritdoc cref="INotificationManager"/>
    public sealed class NotificationManager : INotificationManager<Notification>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly PopupManager<PopupNotification> _popupManager;

        private readonly LinkedList<ManagedNotificationMessage> _notificationMessages
            = new LinkedList<ManagedNotificationMessage>();
        private readonly object _mutex = new object();
        private bool _isDisposed = false;

        /// <summary>
        /// Creates a new instance of the <see cref="NotificationManager"/> type.
        /// </summary>
        /// <param name="dateTimeProvider">The <see cref="IDateTimeProvider"/> used to access the current time.</param>
        /// <param name="optionsProvider">An options provider for the <see cref="NotificationOptions"/> type.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="dateTimeProvider"/> or <paramref name="optionsProvider"/> is <c>null</c>.
        /// </exception>
        public NotificationManager(
            IDateTimeProvider dateTimeProvider,
            IOptions<NotificationOptions> optionsProvider)
        {
            if (dateTimeProvider is null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (optionsProvider is null)
                throw new ArgumentNullException(nameof(optionsProvider));

            _dateTimeProvider = dateTimeProvider;
            _popupManager = new PopupManager<PopupNotification>(dateTimeProvider, optionsProvider);
        }

        /// <inheritdoc />
        public event EventHandler? PopupChanged
        {
            add { _popupManager.CurrentChanged += value; }
            remove { _popupManager.CurrentChanged -= value; }
        }

        /// <inheritdoc />
        public Popup? Popup => _popupManager.Current;

        /// <inheritdoc />
        public void ClearPopup()
        {
            _popupManager.CancelAll();
        }

        /// <inheritdoc />
        public event EventHandler? NotificationsChanged;  // TODO: This keeps alive objects

        private void OnNotificationsChanged()
        {
            NotificationsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public void Dismiss(Notification notification)
        {
            var node = notification.NotificationRef;

            if (node is null || node.List != _notificationMessages)
                return;

            if (!node.Value.AllowDismiss)
                return;

            lock (_mutex)
            {
                if (_isDisposed)
                    return;

                _notificationMessages.Remove(node);
            }

            _popupManager.Cancel(new PopupNotification(node, _dateTimeProvider));
            OnNotificationsChanged();
        }

        /// <inheritdoc />
        public void Dismiss(string? key, Uri? uri)
        {
            var dismissedNotifications = new LinkedList<ManagedNotificationMessage>();

            lock (_mutex)
            {
                if (_isDisposed)
                    return;

                var current = _notificationMessages.First;
                while (current != null)
                {
                    var next = current.Next;

                    if (current.Value.AllowDismiss && MatchesFilter(current, key, uri))
                    {
                        _notificationMessages.Remove(current);
                        dismissedNotifications.AddLast(current);
                    }

                    current = next;
                }
            }

            if (dismissedNotifications.Any())
            {
                OnNotificationsChanged();

                for (var node = dismissedNotifications.First; node != null; node = node.Next)
                {
                    _popupManager.Cancel(new PopupNotification(node, _dateTimeProvider));
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<Notification> GetNotifications(string? key, Uri? uri)
        {
            lock (_mutex)
            {
                CheckDisposed();

                if (_notificationMessages.Count == 0)
                {
                    return ImmutableList<Notification>.Empty;
                }

                if (_notificationMessages.Count == 1)
                {
                    return ImmutableList.Create(new Notification(_notificationMessages.First!, _dateTimeProvider));
                }

                var builder = ImmutableList.CreateBuilder<Notification>();

                for (var current = _notificationMessages.Last; current != null; current = current.Previous)
                {
                    if (!MatchesFilter(current, key, uri))
                    {
                        continue;
                    }

                    builder.Add(new Notification(current, _dateTimeProvider));
                }

                return builder.ToImmutable();
            }
        }

        private static bool MatchesFilter(LinkedListNode<ManagedNotificationMessage> node, string? key, Uri? uri)
        {
            if (key != null && node.Value.Key != key)
            {
                return false;
            }

            if (uri != null && !node.Value.UriFilter.IsMatch(uri))
            {
                return false;
            }

            return true;
        }

        private void PlaceNotification(LinkedListNode<ManagedNotificationMessage> node)
        {
            if (node.Value.Expiration is null)
            {
                lock (_mutex)
                {
                    CheckDisposed();
                    _notificationMessages.AddLast(node);
                }

                _popupManager.Schedule(new PopupNotification(node, _dateTimeProvider));
                OnNotificationsChanged();

                return;
            }

            var now = _dateTimeProvider.GetCurrentTime();
            var delay = (DateTime)node.Value.Expiration - now;

            // The message is expired already, do not add it.
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            // We have to add the message before scheduling the continuation
            // to prevent a race when delay is small and the continuation is
            // invoked before the message is added actually.
            lock (_mutex)
            {
                CheckDisposed();
                _notificationMessages.AddLast(node);
            }

            _popupManager.Schedule(new PopupNotification(node, _dateTimeProvider));
            OnNotificationsChanged();

            async Task RemoveNotificationAfterDelayAsync()
            {
                do
                {
                    await _dateTimeProvider.DelayAsync(delay).ConfigureAwait(false);
                    now = _dateTimeProvider.GetCurrentTime();
                    delay = (DateTime)node.Value.Expiration - now;
                }
                while (delay > TimeSpan.Zero);

                var notificationRemoved = false;

                lock (_mutex)
                {
                    // The notification may already be removed in the meantime.
                    if (!_isDisposed && node.List == _notificationMessages)
                    {
                        _notificationMessages.Remove(node);
                        notificationRemoved = true;
                    }
                }

                if (notificationRemoved)
                {
                    _popupManager.Cancel(new PopupNotification(node, _dateTimeProvider));
                    OnNotificationsChanged();
                }
            }

            RemoveNotificationAfterDelayAsync().HandleExceptions();
        }

        /// <inheritdoc />
        public NotificationPlacement PlaceNotification(NotificationMessage notificationMessage)
        {
            if (notificationMessage is null)
                throw new ArgumentNullException(nameof(notificationMessage));

            if (notificationMessage.NotificationType == NotificationType.None)
            {
                return default;
            }

            var managedMessage = new ManagedNotificationMessage(notificationMessage, this, _dateTimeProvider);
            var node = new LinkedListNode<ManagedNotificationMessage>(managedMessage);
            PlaceNotification(node);
            return new NotificationPlacement(this, node);
        }

        /// <inheritdoc />
        public void CancelNotification(in NotificationPlacement notificationPlacement)
        {
            if (notificationPlacement.NotificationManager != this)
            {
                if (notificationPlacement.IsOfScopedNotificationManager(this))
                {
                    notificationPlacement.NotificationManager.CancelNotification(notificationPlacement);
                }
            }
            else if (notificationPlacement.NotificationRef is LinkedListNode<ManagedNotificationMessage> node)
            {
                lock (_mutex)
                {
                    CheckDisposed();
                    _notificationMessages.Remove(node);
                    _popupManager.Cancel(new PopupNotification(node, _dateTimeProvider));
                }

                OnNotificationsChanged();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_mutex)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;

                _notificationMessages.Clear();
                OnNotificationsChanged();
            }
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        INotificationManagerScope INotificationManager.CreateScope()
        {
            return CreateScope();
        }

        INotificationRecorder INotificationManager.CreateRecorder()
        {
            return CreateRecorder();
        }

        /// <inheritdoc />
        public NotificationManagerScope CreateScope()
        {
            return new NotificationManagerScope(this);
        }

        /// <inheritdoc />
        public NotificationRecorder CreateRecorder()
        {
            return new NotificationRecorder(this);
        }
    }
}
