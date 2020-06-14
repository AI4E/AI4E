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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using AI4E.Messaging.Validation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AI4E.AspNetCore.Components
{
    public class ValidationResultsValidator : ComponentBase
    {
        private ValidationMessageStore? _messages;
        private ImmutableHashSet<ValidationResult> _validationResults = ImmutableHashSet<ValidationResult>.Empty;

        [CascadingParameter] private EditContext? CurrentEditContext { get; set; }

        [Parameter] public IEnumerable<ValidationResult> ValidationResults { get; set; } = Enumerable.Empty<ValidationResult>();
        [Parameter] public EventCallback<EditContext> OnValidationRequested { get; set; }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            if (CurrentEditContext == null)
            {
                throw new InvalidOperationException($"{nameof(ValidationResultsValidator)} requires a cascading " +
                    $"parameter of type {nameof(EditContext)}. For example, you can use {nameof(ValidationResultsValidator)} " +
                    $"inside an EditForm.");
            }

            _messages = new ValidationMessageStore(CurrentEditContext);

            // Perform object-level validation on request
            CurrentEditContext.OnValidationRequested += (sender, eventArgs) => ValidateModel();

            // Perform per-field validation on each field edit
            CurrentEditContext.OnFieldChanged += (sender, eventArgs) => ValidateModel();
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            if (!_validationResults.SetEquals(ValidationResults))
            {
                UpdateValidationResults();
            }
        }

        private void UpdateValidationResults()
        {
            Debug.Assert(CurrentEditContext != null);
            Debug.Assert(_messages != null);

            _validationResults = (ValidationResults as ImmutableHashSet<ValidationResult>)
                ?? ValidationResults?.ToImmutableHashSet()
                ?? ImmutableHashSet<ValidationResult>.Empty;

            // Transfer results to the ValidationMessageStore
            _messages!.Clear();
            foreach (var validationResult in _validationResults)
            {
                if (validationResult.Member is null)
                {
                    _messages.Add(
                        new FieldIdentifier(
                            CurrentEditContext!.Model, fieldName: string.Empty), validationResult.Message);
                }
                else
                {
                    _messages.Add(CurrentEditContext!.Field(validationResult.Member), validationResult.Message);
                }
            }

            CurrentEditContext!.NotifyValidationStateChanged();
        }

        private async void ValidateModel()
        {
            Debug.Assert(CurrentEditContext != null);

            if (!OnValidationRequested.HasDelegate)
            {
                return;
            }

            await OnValidationRequested.InvokeAsync(CurrentEditContext!).ConfigureAwait(true);
        }
    }
}
