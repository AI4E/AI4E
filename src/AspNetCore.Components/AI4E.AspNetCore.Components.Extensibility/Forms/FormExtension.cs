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

using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AI4E.AspNetCore.Components.Forms
{
    /// <summary>
    /// A handle for a form-extension.
    /// </summary>
#pragma warning disable CA1815
    public readonly struct FormExtension : IDisposable
#pragma warning restore CA1815
    {
        internal FormExtension(
            ExtensibleEditContext extensibleEditContext,
            EditContext editContext,
            EventCallback<EditContext> onInvalidSubmit,
            EventCallback<EditContext> onValidSubmit,
            int seqNum)
        {
            if (extensibleEditContext is null)
                throw new ArgumentNullException(nameof(extensibleEditContext));

            if (editContext is null)
                throw new ArgumentNullException(nameof(editContext));

            ExtensibleEditContext = extensibleEditContext;
            EditContext = editContext;
            OnInvalidSubmit = onInvalidSubmit;
            OnValidSubmit = onValidSubmit;
            SeqNum = seqNum;
        }


        /// <summary>
        /// Gets the edit-context of the form-extension.
        /// </summary>
        public EditContext EditContext { get; }

        /// <summary>
        /// Gets the event-callback that shall be invoked on invalid submits.
        /// </summary>
        public EventCallback<EditContext> OnInvalidSubmit { get; }

        /// <summary>
        /// Gets the event-callback that shall be invoked on valid submits.
        /// </summary>
        public EventCallback<EditContext> OnValidSubmit { get; }

        internal ExtensibleEditContext ExtensibleEditContext { get; }
        internal int SeqNum { get; }

        /// <summary>
        /// Unregistered the form-extension from the respective edit-context.
        /// </summary>
        public void Dispose()
        {
            if (EditContext != null)
                ExtensibleEditContext.UnregisterEditFormExtension(this);
        }
    }
}
