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
using AI4E.AspNetCore.Components.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Notifications.Sample.Shared
{
    /// <summary>
    /// A base component for rendering notifications.
    /// </summary>
    public abstract class NotificationComponent : ComponentBase, IDisposable
    {
        private IReadOnlyList<Notification>? _notifications;
        private bool _locationChangedCallbackRegistered;
        private bool _disposed = false;

#nullable disable annotations
        [Inject] private INotificationManager<Notification> NotificationManager { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }
#nullable enable annotations

        /// <summary>
        /// Gets or sets the key of the notifications to render or <c>null</c> to render notifications regardless of 
        /// their key.
        /// </summary>
        /// <remarks>
        /// This filters the notification by key.
        /// </remarks>
        [Parameter] public string? Key { get; set; }

        /// <summary>
        /// Gets or sets a boolean value indicating whether notifications shall be filtered based on the current 
        /// location.
        /// </summary>
        [Parameter] public bool FilterOnCurrentLocation { get; set; }

        /// <summary>
        /// Gets the collection of available notifications.
        /// </summary>
        protected IReadOnlyList<Notification> Notifications => _notifications ?? Array.Empty<Notification>();

        /// <inheritdoc/>
        protected override void OnParametersSet()
        {
            UpdateNotifications();

            if (FilterOnCurrentLocation != _locationChangedCallbackRegistered)
            {
                if (FilterOnCurrentLocation)
                {
                    NavigationManager.LocationChanged += OnLocationChanged;
                }
                else
                {
                    NavigationManager.LocationChanged -= OnLocationChanged;
                }

                _locationChangedCallbackRegistered = FilterOnCurrentLocation;
            }
        }

        /// <inheritdoc/>
        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                NotificationManager.NotificationsChanged += OnNotificationsChanged;
            }
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            UpdateNotifications();
        }

        private void OnNotificationsChanged(object? sender, EventArgs e)
        {
            _ = InvokeAsync(() =>
            {
                UpdateNotifications();
            });
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Provides a mechanism for releasing resources.
        /// </summary>
        /// <param name="disposing">A boolean value indicating whether managed resources shall be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                NotificationManager.NotificationsChanged -= OnNotificationsChanged;
            }

            _disposed = true;
        }

        private void UpdateNotifications()
        {
            // We use the string overload, as we get a string from the navigation manager and do not want to 
            // copy the conversion logic here but rely in the notification manager to handle this.
#pragma warning disable CA2234
            var notifications = NotificationManager.GetNotifications(Key, NavigationManager.Uri);
#pragma warning restore CA2234

            // Only update state if notifications actually changed!
            if (_notifications is null || !notifications.ScrambledEquals(_notifications))
            {
                _notifications = notifications;
                StateHasChanged();
            }
        }
    }
}
