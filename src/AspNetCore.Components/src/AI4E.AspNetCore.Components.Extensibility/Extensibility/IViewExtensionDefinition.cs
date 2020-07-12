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

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace AI4E.AspNetCore.Components.Extensibility
{
    /// <summary>
    /// Marks types to be view extension definition or implementing a view extension defintion.
    /// </summary>
    public interface IViewExtensionDefinition : IComponent { } // TODO: Rename to IViewExtension

    /// <summary>
    /// Marks types to be view extension definition or implementing a view extension defintion.
    /// </summary>
    /// <typeparam name="TContext">The type of context parameter.</typeparam>
    public interface IViewExtensionDefinition<TContext> : IViewExtensionDefinition  // TODO: Rename to IViewExtension
    {
        /// <summary>
        /// Gets or sets the view-extension context.
        /// </summary>
        [MaybeNull] TContext Context { get; set; }
    }
}
