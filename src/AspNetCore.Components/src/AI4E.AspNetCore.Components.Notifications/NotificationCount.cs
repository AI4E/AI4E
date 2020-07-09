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

namespace Notifications.Sample.Shared
{
    /// <summary>
    /// A component that renders the number of available notifications.
    /// </summary>
    public sealed class NotificationCount : NotificationComponent
    {
        /// <summary>
        /// Gets or sets the template that renders the notification count.
        /// </summary>
        [Parameter] public RenderFragment<int>? Template { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            builder.AddContent(0, Template, Notifications.Count);
        }
    }
}
