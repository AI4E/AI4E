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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AI4E.AspNetCore.Components.Extensibility
{
    /// <summary>
    /// A base type for view extensions.
    /// </summary>
    /// <remarks>
    /// A view extension can alternatively beeing rendered via
    /// the <see cref="ViewExtensionPlaceholder{TViewExtension}"/> component.
    /// </remarks>
    public abstract class ViewExtensionBase : ComponentBase, IViewExtensionDefinition
    {
        private IReadOnlyDictionary<string, object?>? _parameters;

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            builder.OpenComponent(sequence: 0, typeof(ViewExtensionPlaceholder<>).MakeGenericType(GetType()));
            if (_parameters != null)
            {
                builder.AddMultipleAttributes(sequence: 0, _parameters);
            }
            builder.CloseComponent();
        }

        /// <inheritdoc />
        public override Task SetParametersAsync(ParameterView parameters)
        {
            _parameters = parameters.ToDictionary();
            return base.SetParametersAsync(parameters);
        }
    }

    /// <summary>
    /// A generic base type for view extensions.
    /// </summary>
    /// <remarks>
    /// A view extension can alternatively beeing rendered via
    /// the <see cref="ViewExtensionPlaceholder{TViewExtension}"/> component.
    /// </remarks>
    /// <typeparam name="TContext">The type of context parameter.</typeparam>
    public abstract class ViewExtensionBase<TContext> : ViewExtensionBase, IViewExtensionDefinition<TContext>
    {
        /// <inheritdoc />
        [MaybeNull, Parameter] public TContext Context { get; set; } = default!;
    }
}
