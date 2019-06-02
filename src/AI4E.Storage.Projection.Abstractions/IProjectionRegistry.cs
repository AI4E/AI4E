/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IHandlerRegistry.cs 
 * Types:           AI4E.Storage.Projection.IHandlerRegistry'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

using System.Collections.Generic;

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Represents a handler registry.
    /// </summary>
    /// <typeparam name="THandler">The type of handler.</typeparam>
    public interface IProjectionRegistry<THandler>
    {
        /// <summary>
        /// Registers a handler.
        /// </summary>
        /// <param name="provider">The handler to register.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="provider"/> is null.</exception>
        bool Register(IContextualProvider<THandler> provider);

        /// <summary>
        /// Unregisters a handler.
        /// </summary>
        /// <param name="provider">The handler to unregister.</param>
        /// <returns>
        /// A boolean value indicating whether the handler was actually found and unregistered.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="provider"/> is null.</exception>
        bool Unregister(IContextualProvider<THandler> provider);

        /// <summary>
        /// Tries to retrieve the latest, registered handler.
        /// </summary>
        /// <param name="provider">Contains the handler if true is returned, otherwise the value is undefined.</param>
        /// <returns>True if a handler was found, false otherwise.</returns>
        bool TryGetHandler(out IContextualProvider<THandler> provider);

        /// <summary>
        /// Gets the collection of registered handlers.
        /// </summary>
        IEnumerable<IContextualProvider<THandler>> Handlers { get; }
    }
}
