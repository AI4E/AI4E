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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Notifications
{
#pragma warning disable CA1001
    internal sealed class ManagedPopup<TPopupNotification> : IManagedPopup
        where TPopupNotification : IPopupNotification, IEquatable<TPopupNotification>
#pragma warning restore CA1001
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly Action<ManagedPopup<TPopupNotification>> _scheduleNext;

        private readonly CancellationTokenSource _cancellationSource;
        private Task? _task;

        public ManagedPopup(
            TPopupNotification popupNotification,
            TimeSpan popupDuration,
            IDateTimeProvider dateTimeProvider,
            Action<ManagedPopup<TPopupNotification>> scheduleNext)
        {
            PopupNotification = popupNotification;
            _dateTimeProvider = dateTimeProvider;
            _scheduleNext = scheduleNext;
            _cancellationSource = new CancellationTokenSource();

            var now = _dateTimeProvider.GetCurrentTime();
            PopupExpiration = now + popupDuration;

            if (popupNotification.Expiration != null && popupNotification.Expiration < PopupExpiration)
            {
                PopupExpiration = popupNotification.Expiration.Value;
            }
        }

        public TPopupNotification PopupNotification { get; }
        public DateTime PopupExpiration { get; }
        public Notification Notification => PopupNotification.Notification;

        public void Start()
        {
            if (_task != null)
                return;

            var now = _dateTimeProvider.GetCurrentTime();
            var delay = PopupExpiration - now;

            _task = ScheduleNextAfterDurationAsync();
            return;

            async Task? ScheduleNextAfterDurationAsync()
            {
                using (_cancellationSource)
                {
                    CancellationToken cancellation;

                    try
                    {
                        cancellation = _cancellationSource.Token;
                    }
                    catch (ObjectDisposedException)
                    {
                        _scheduleNext(this);
                        return;
                    }

                    do
                    {
                        try
                        {
                            await _dateTimeProvider.DelayAsync(delay, cancellation).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                        {
                            _scheduleNext(this);
                            return;
                        }

                        now = _dateTimeProvider.GetCurrentTime();
                        delay = PopupExpiration - now;
                    }
                    while (delay > TimeSpan.Zero);

                    _scheduleNext(this);
                }
            }
        }

        public void Cancel()
        {
            using (_cancellationSource)
            {
                try
                {
                    _cancellationSource.Cancel();
                }
                catch (ObjectDisposedException) { }
            }
        }
    }
}
