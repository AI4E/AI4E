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
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <summary>
    /// A component that renders a dismiss-all button.
    /// </summary>
    public sealed class DismissNotificationsButton : NotificationComponent
    {
        /// <summary>
        /// Gets or sets the button template.
        /// </summary>
        [Parameter] public RenderFragment<DismissNotificationButtonContext>? Template { get; set; }

        /// <inheritdoc/>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            var uriFilter = UriFilter;

            var context = new DismissNotificationButtonContext(
                NotificationManager,
                canDismiss: Notifications.Any(p => p.AllowDismiss),
                Key,
                UriFilter);

            builder.AddContent(0, Template, context);
        }
    }

    /// <summary>
    /// Represents the context of the <see cref="DismissNotificationsButton"/> component.
    /// </summary>
    public sealed class DismissNotificationButtonContext
    {
        private readonly INotificationManager<Notification> _notificationManager;
        private readonly string? _key;
        private readonly Uri? _uri;

        internal DismissNotificationButtonContext(
            INotificationManager<Notification> notificationManager,
            bool canDismiss,
            string? key,
            Uri? uri)
        {
            if (notificationManager is null)
                throw new ArgumentNullException(nameof(notificationManager));

            _notificationManager = notificationManager;
            CanDismiss = canDismiss;
            _key = key;
            _uri = uri;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the notification manager contains any dismissable notifications 
        /// in the current context.
        /// </summary>
        public bool CanDismiss { get; }

        /// <summary>
        /// Dismisses all dismissable notifications in the current context.
        /// </summary>
        public void Dismiss()
        {
            _notificationManager.Dismiss(_key, _uri);
        }
    }
}
