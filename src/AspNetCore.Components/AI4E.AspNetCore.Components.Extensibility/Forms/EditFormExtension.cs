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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace AI4E.AspNetCore.Components.Forms
{
    /// <summary>
    /// Extensions an <see cref="ExtensibleEditForm"/>.
    /// </summary>
    public class EditFormExtension : ExtensibleEditFormBase, IDisposable
    {
        private EditContext? _fixedEditContext;
        private FormExtension? _registration;

        /// <summary>
        /// Gets or sets the cascading extendible edit context.
        /// </summary>
        [CascadingParameter] public ExtensibleEditContext? ExtensibleEditContext { get; set; }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
#pragma warning disable IDE0047
            if ((EditContext == null) == (Model == null))
#pragma warning restore IDE0047
            {
                throw new InvalidOperationException($"{nameof(EditFormExtension)} requires a {nameof(Model)} " +
                    $"parameter, or an {nameof(EditContext)} parameter, but not both.");
            }

            if (ExtensibleEditContext == null)
            {
                throw new InvalidOperationException("The ExtensibleEditContext parameter must be set.");
            }

            // Update _fixedEditContext if we don't have one yet, or if they are supplying a
            // potentially new EditContext, or if they are supplying a different Model
            if (_fixedEditContext == null || EditContext != null || Model != _fixedEditContext.Model)
            {
                if (_registration != null)
                {
                    ExtensibleEditContext.UnregisterEditFormExtension(_registration.Value);
                }

                _fixedEditContext = EditContext ?? new EditContext(Model);
                _registration = ExtensibleEditContext.RegisterEditFormExtension(_fixedEditContext, OnInvalidSubmit, OnValidSubmit);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

#pragma warning disable IDE0060
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">A boolean value indicating whether the instance is disposing.</param>
        protected virtual void Dispose(bool disposing)
#pragma warning restore IDE0060
        {
            if (_registration != null)
            {
                ExtensibleEditContext?.UnregisterEditFormExtension(_registration.Value);
            }
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            var sequence = 0;

            Debug.Assert(ExtensibleEditContext != null);
            Debug.Assert(_fixedEditContext != null);

            void BuildEditContextCascadingValue(RenderTreeBuilder builder)
            {
                builder.OpenComponent<CascadingValue<EditContext>>(sequence++);
                builder.AddAttribute(sequence++, nameof(CascadingValue<EditContext>.IsFixed), true);
                builder.AddAttribute(sequence++, nameof(CascadingValue<EditContext>.Value), _fixedEditContext);
                builder.AddAttribute(sequence++, nameof(CascadingValue<EditContext>.ChildContent), ChildContent?.Invoke(ExtensibleEditContext!));
                builder.CloseComponent();
            }

            // If _fixedEditContext changes, tear down and recreate all descendants.
            // This is so we can safely use the IsFixed optimization on CascadingValue,
            // optimizing for the common case where _fixedEditContext never changes.
            builder.OpenRegion(_fixedEditContext!.GetHashCode());

            builder.OpenElement(sequence++, "div");
            builder.AddMultipleAttributes(sequence++, AdditionalAttributes);
            BuildEditContextCascadingValue(builder);
            builder.CloseElement();

            builder.CloseRegion();
        }
    }
}
