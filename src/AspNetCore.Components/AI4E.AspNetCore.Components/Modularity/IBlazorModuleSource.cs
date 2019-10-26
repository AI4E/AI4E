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
using System.Threading;

namespace AI4E.AspNetCore.Components.Modularity
{
    /// <summary>
    /// Represents a source of all known blazor-modules.
    /// </summary>
    /// <remarks>
    /// Implementors must guarantee thread-safety for all members.
    /// </remarks>
    public interface IBlazorModuleSource
    {
        /// <summary>
        /// Returns an <see cref="IAsyncEnumerable{T}"/> of descriptors of all known blazor-modules.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operator,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        IAsyncEnumerable<BlazorModuleDescriptor> GetModulesAsync(CancellationToken cancellation);

        /// <summary>
        /// Notifies when the collection of known blazor-modules changes.
        /// </summary>
        event EventHandler? ModulesChanged;
    }
}
