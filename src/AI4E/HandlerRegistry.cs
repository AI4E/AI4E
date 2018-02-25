/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        HandlerRegistry.cs 
 * Types:           AI4E.HandlerRegistry'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   25.02.2018 
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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace AI4E
{
    /// <summary>
    /// Represents an asychronous registry with multiple handlers activated at once.
    /// </summary>
    /// <typeparam name="THandler">The type of handler.</typeparam>
    public sealed class HandlerRegistry<THandler> : IHandlerRegistry<THandler>
    {
        private volatile ImmutableList<IContextualProvider<THandler>> _handlers = ImmutableList<IContextualProvider<THandler>>.Empty;

        /// <summary>
        /// Creates a new instance of the <see cref="AsyncSingleHandlerRegistry{THandler}"/> type.
        /// </summary>
        public HandlerRegistry() { }

        /// <summary>
        /// Registers a handler.
        /// </summary>
        /// <param name="provider">The handler to register.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="provider"/> is null.</exception>
        public void Register(IContextualProvider<THandler> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Debug.Assert(_handlers != null);

            ImmutableList<IContextualProvider<THandler>> current = _handlers, // Volatile read op.
                                                              start,
                                                              desired;
            do
            {
                desired = start = current;

                if (!start.IsEmpty)
                {
                    var tos = start.Last();

                    Debug.Assert(tos != null);

                    desired = start.Remove(handler);
                }

                desired = desired.Add(handler);

                current = Interlocked.CompareExchange(ref _handlers, desired, start);
            }
            while (start != current);
        }

        /// <summary>
        /// Unregisters a handler.
        /// </summary>
        /// <param name="provider">The handler to unregister.</param>
        /// <returns>
        /// A boolean value indicating whether the handler was actually found and unregistered.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="provider"/> is null.</exception>
        public bool Unregister(IContextualProvider<THandler> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Debug.Assert(_handlers != null);

            ImmutableList<IContextualProvider<THandler>> current = _handlers, // Volatile read op.
                                                         start,
                                                         desired;

            do
            {
                start = current;

                // If no handlers are present, we cannot remove anything.
                if (start.IsEmpty)
                {
                    return false;
                }

                // Read the top of stack
                var tos = start.Last();

                // If handlers are present, there has to be a top of stack.
                Debug.Assert(tos != null);

                // If the handler to remove is on top of stack, remove the top of stack.
                if (handler.Equals(tos))
                {
                    desired = start.RemoveAt(start.Count - 1);
                }
                else
                {
                    desired = start.Remove(handler);

                    if (desired == start)
                        return false;
                }

                current = Interlocked.CompareExchange(ref _handlers, desired, start);
            }
            while (start != current);

            return true;
        }

        /// <summary>
        /// Tries to retrieve the currently activated handler.
        /// </summary>
        /// <param name="handler">Contains the handler if true is returned, otherwise the value is undefined.</param>
        /// <returns>True if a handler was found, false otherwise.</returns>
        public bool TryGetHandler(out IContextualProvider<THandler> handler)
        {
            var handlers = _handlers; // Volatile read op.

            Debug.Assert(handlers != null);

            if (handlers.IsEmpty)
            {
                handler = null;
                return false;
            }

            handler = handlers.Last();
            return true;
        }

        public IEnumerable<IContextualProvider<THandler>> Handlers => _handlers; // Volatile read op.
    }
}
