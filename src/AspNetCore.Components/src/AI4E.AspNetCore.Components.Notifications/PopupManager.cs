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
using AI4E.Utils;
using Microsoft.Extensions.Options;

namespace AI4E.AspNetCore.Components.Notifications
{
    internal sealed class PopupManager<TPopupNotification>
        where TPopupNotification : IPopupNotification, IEquatable<TPopupNotification>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IOptions<NotificationOptions> _optionsProvider;

        private readonly object _mutex = new object();
        private readonly LinkedList<TPopupNotification> _queue = new LinkedList<TPopupNotification>();
        private ManagedPopup<TPopupNotification>? _current = null;

        public PopupManager(IDateTimeProvider dateTimeProvider, IOptions<NotificationOptions> optionsProvider)
        {
            if (dateTimeProvider is null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (optionsProvider is null)
                throw new ArgumentNullException(nameof(optionsProvider));

            _dateTimeProvider = dateTimeProvider;
            _optionsProvider = optionsProvider;
        }

        public event EventHandler? CurrentChanged;

        private void OnCurrentChanged()
        {
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }

        public Popup? Current
        {
            get
            {
                lock (_mutex)
                {
                    if (_current is null)
                        return null;

                    return new Popup(_current);
                }
            }
        }

        public void Schedule(TPopupNotification notification)
        {
            if (!notification.ShowPopup)
                return;

            lock (_mutex)
            {
                _queue.AddLast(notification);
            }

            ScheduleNext(null);
        }

        private ManagedPopup<TPopupNotification> CreatePopup(TPopupNotification notification)
        {
            return new ManagedPopup<TPopupNotification>(
                notification,
                _optionsProvider.Value.PopupDuration,
                _dateTimeProvider,
                ScheduleNext);
        }

        private void ScheduleNext(ManagedPopup<TPopupNotification>? current)
        {
            ManagedPopup<TPopupNotification>? popup = null;

            lock (_mutex)
            {
                if (_current != current)
                {
                    return;
                }

                for (var first = _queue.First; first != null; first = _queue.First)
                {
                    _queue.RemoveFirst();
                    popup = CreatePopup(first.Value);

                    var now = _dateTimeProvider.GetCurrentTime();
                    var delay = popup.PopupExpiration - now;

                    if (delay > TimeSpan.Zero)
                    {
                        break;
                    }
                }

                _current = popup;
            }

            popup?.Start();
            OnCurrentChanged();
        }

        public void Cancel(TPopupNotification notification)
        {
            if (!notification.ShowPopup)
                return;

            ManagedPopup<TPopupNotification>? toCancel = null;

            lock (_mutex)
            {
                if (_current is null)
                {
                    return;
                }

                if (_current.PopupNotification.Equals(notification))
                {
                    toCancel = _current;
                }
                else
                {
                    var current = _queue.First;

                    while (current != null)
                    {
                        if (current.Value.Equals(notification))
                        {
                            _queue.Remove(current);
                        }

                        current = current.Next;
                    }
                }
            }

            if (toCancel != null)
            {
                toCancel.Cancel();
            }
        }

        public void CancelAll()
        {
            ManagedPopup<TPopupNotification>? toCancel = null;

            lock (_mutex)
            {
                toCancel = _current;
                _queue.Clear();
            }

            if (toCancel != null)
            {
                toCancel.Cancel();
            }
        }
    }
}
