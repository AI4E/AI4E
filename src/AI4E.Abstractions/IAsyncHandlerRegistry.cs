/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IAsyncHandlerRegistry.cs 
 * Types:           AI4E.IAsyncHandlerRegistry'1
 *                  AI4E.IAsyncSingleHandlerRegistry'1
 *                  AI4E.IAsyncMultipleHandlerRegistry'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   09.05.2017 
 * Status:          Ready
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
using System.Threading.Tasks;

namespace AI4E
{
    /// <summary>
    /// Represents an asynchronous handler registry.
    /// </summary>
    /// <typeparam name="THandler">The type of handler.</typeparam>
    public interface IAsyncHandlerRegistry<in THandler>
    {
        /// <summary>
        /// Asynchronously registers a handler.
        /// </summary>
        /// <param name="handlerFactory">The handler to register.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="handlerFactory"/> is null.</exception>
        Task RegisterAsync(IContextualProvider<THandler> handlerFactory);

        /// <summary>
        /// Asynchronously deregisters a handler.
        /// </summary>
        /// <param name="handlerFactory">The handler to deregister.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The value of the <see cref="Task{TResult}.Result"/> parameter contains a boolean value
        /// indicating whether the handler was actually found and deregistered.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="handlerFactory"/> is null.</exception>
        Task<bool> DeregisterAsync(IContextualProvider<THandler> handlerFactory);
    }

    /// <summary>
    /// Represents an asynchronous handler registry with only a single handler activated at once.
    /// </summary>
    /// <typeparam name="THandler">The type of handler.</typeparam>
    public interface IAsyncSingleHandlerRegistry<THandler> : IAsyncHandlerRegistry<THandler>
    {
        /// <summary>
        /// Tries to retrieve the currently activated handler.
        /// </summary>
        /// <param name="handlerFactory">Contains the handler if true is returned, otherwise the value is undefined.</param>
        /// <returns>True if a handler was found, false otherwise.</returns>
        bool TryGetHandler(out IContextualProvider<THandler> handlerFactory);
    }

    /// <summary>
    /// Represents an asychronous registry with multiple handlers activated at once.
    /// </summary>
    /// <typeparam name="THandler">The type of handler.</typeparam>
    public interface IAsyncMultipleHandlerRegistry<THandler> : IAsyncHandlerRegistry<THandler>
    {
        /// <summary>
        /// Returns a collection if activated handlers.
        /// </summary>
        /// <returns>The collection of activated handlers.</returns>
        IEnumerable<IContextualProvider<THandler>> GetHandlers(); // TODO: Replace with property?
    }
}
