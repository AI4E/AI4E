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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AI4E.AspNetCore.Components.Notifications
{
    /// <summary>
    /// A component that renders a list of available notifications.
    /// </summary>
    public sealed class NotificationList : NotificationComponent
    {
        /// <summary>
        /// Gets or sets the notification template that renders a single notification.
        /// </summary>
        [Parameter] public RenderFragment<Notification>? NotificationTemplate { get; set; }

        /// <summary>
        /// Gets or sets the template that shall be rendered if no notification is available.
        /// </summary>
        [Parameter] public RenderFragment? NoNotificationsTemplate { get; set; }

        /// <summary>
        /// Gets or sets the template that shall be rendered if notification are available but no one matched the 
        /// filter.
        /// </summary>
        [Parameter] public RenderFragment? NoMatchingNotificationsTemplate { get; set; }

        /// <summary>
        /// Gets or sets a predicate to filter the notifications list.
        /// </summary>
        [Parameter] public Func<Notification, bool>? Filter { get; set; }

        /// <inheritdoc/>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            if (Notifications.Count == 0)
            {
                builder.AddContent(0, NoNotificationsTemplate);
            }
            else
            {
                if (NotificationTemplate is null)
                    return;

                var atLeasedOneMatched = false;

                for (var i = 0; i < Notifications.Count; i++)
                {
                    var notification = Notifications[i];

                    if (Filter is null || Filter(notification))
                    {
                        atLeasedOneMatched = true;

                        builder.OpenElement(0, "div");
                        builder.SetKey(notification);
                        builder.AddContent(0, NotificationTemplate, notification);
                        builder.CloseElement();
                    }
                }

                if(!atLeasedOneMatched)
                {
                    builder.AddContent(0, NoMatchingNotificationsTemplate ?? NoNotificationsTemplate);
                }
            }
        }
    }
}
