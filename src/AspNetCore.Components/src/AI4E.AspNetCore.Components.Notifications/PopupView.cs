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
    public sealed class PopupView : ComponentBase, IDisposable
    {
#nullable disable annotations
        [Inject] private INotificationManager<Notification> NotificationManager { get; set; }
#nullable enable annotations

        /// <summary>
        /// Gets or sets the popup template that renders a popup.
        /// </summary>
        [Parameter] public RenderFragment<Popup>? PopupTemplate { get; set; }

        /// <summary>
        /// Gets or sets the template that is rendered when no popup is available.
        /// </summary>
        [Parameter] public RenderFragment? NoPopupTemplate { get; set; }

        /// <inheritdoc/>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            var popup = NotificationManager.Popup;

            if (popup is null)
            {
                builder.AddContent(0, NoPopupTemplate);
            }
            else
            {
                builder.AddContent(0, PopupTemplate, popup.Value);
            }
        }

        /// <inheritdoc/>
        protected override void OnParametersSet()
        {
            StateHasChanged();
        }

        /// <inheritdoc/>
        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                NotificationManager.PopupChanged += OnPopupChanged;
            }
        }

        private void OnPopupChanged(object? sender, EventArgs e)
        {
            _ = InvokeAsync(() =>
            {
                StateHasChanged();
            });
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            NotificationManager.PopupChanged -= OnPopupChanged;
        }
    }
}
