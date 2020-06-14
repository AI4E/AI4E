/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * AspNet Core (https://github.com/aspnet/AspNetCore)
 * Copyright (c) .NET Foundation. All rights reserved.
 * Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace AI4E.AspNetCore.Components.Forms
{
    /// <summary>
    /// An edit form that can be extended by <see cref="EditFormExtension"/>s.
    /// </summary>
    public class ExtensibleEditForm : ExtensibleEditFormBase
    {
        private readonly Func<Task> _handleSubmitDelegate; // Cache to avoid per-render allocations
        private ExtensibleEditContext? _extensibleEditContext;

        /// <summary>
        /// Creates a new instance of the <see cref="ExtensibleEditForm"/> component.
        /// </summary>
        public ExtensibleEditForm()
        {
            _handleSubmitDelegate = HandleSubmitAsync;
        }


        private EditContext BuildEditContext()
        {
            return EditContext ?? new EditContext(Model);
        }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
#pragma warning disable IDE0047
            if ((EditContext == null) == (Model == null))
#pragma warning restore IDE0047
            {
                throw new InvalidOperationException($"{nameof(EditForm)} requires a {nameof(Model)} " +
                    $"parameter, or an {nameof(EditContext)} parameter, but not both.");
            }

            // Update _fixedEditContext if we don't have one yet, or if they are supplying a
            // potentially new EditContext, or if they are supplying a different Model
            if (_extensibleEditContext == null)
            {
                _extensibleEditContext = new ExtensibleEditContext(BuildEditContext());
            }
            else if (EditContext != null || Model != _extensibleEditContext.RootEditContext.Model)
            {
                _extensibleEditContext.RootEditContext = BuildEditContext();
            }
        }

#pragma warning disable CA2007
        private async Task HandleSubmitAsync()
        {
            Debug.Assert(_extensibleEditContext != null);
            var isValid = _extensibleEditContext!.Validate();

            if (isValid)
            {
                if (OnValidSubmit.HasDelegate)
                {

                    await OnValidSubmit.InvokeAsync(_extensibleEditContext.RootEditContext);

                }

                await _extensibleEditContext.OnValidSubmit();
            }
            else
            {
                if (OnInvalidSubmit.HasDelegate)
                {
                    await OnInvalidSubmit.InvokeAsync(_extensibleEditContext.RootEditContext);
                }

                await _extensibleEditContext.OnInvalidSubmit();
            }
        }
#pragma warning restore CA2007

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            Debug.Assert(_extensibleEditContext != null);

            var sequence = 0;
            var editContext = _extensibleEditContext!.RootEditContext;

            void BuildEditContextCascadingValue(RenderTreeBuilder builder)
            {
                builder.OpenComponent<CascadingValue<EditContext>>(sequence++);
                builder.AddAttribute(sequence++, nameof(CascadingValue<EditContext>.IsFixed), true);
                builder.AddAttribute(sequence++, nameof(CascadingValue<EditContext>.Value), editContext);
                builder.AddAttribute(sequence++, nameof(CascadingValue<EditContext>.ChildContent), ChildContent?.Invoke(_extensibleEditContext!));
                builder.CloseComponent();
            }

            void BuildExtensibleEditContextCascadingValue(RenderTreeBuilder builder)
            {
                builder.OpenComponent<CascadingValue<ExtensibleEditContext>>(sequence++);
                builder.AddAttribute(sequence++, nameof(CascadingValue<ExtensibleEditContext>.IsFixed), true);
                builder.AddAttribute(sequence++, nameof(CascadingValue<ExtensibleEditContext>.Value), _extensibleEditContext);
                builder.AddAttribute(sequence++, nameof(CascadingValue<ExtensibleEditContext>.ChildContent), (RenderFragment)BuildEditContextCascadingValue);
                builder.CloseComponent();
            }

            // If editContext changes, tear down and recreate all descendants.
            // This is so we can safely use the IsFixed optimization on CascadingValue,
            // optimizing for the common case where _fixedEditContext never changes.
            builder.OpenRegion(editContext.GetHashCode());

            builder.OpenElement(sequence++, "form");
            builder.AddMultipleAttributes(sequence++, AdditionalAttributes);
            builder.AddAttribute(sequence++, "onsubmit", _handleSubmitDelegate);
            BuildExtensibleEditContextCascadingValue(builder);
            builder.CloseElement();

            builder.CloseRegion();
        }
    }
}
