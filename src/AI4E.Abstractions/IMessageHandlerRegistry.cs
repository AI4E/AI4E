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

namespace AI4E
{
    /// <summary>
    /// Represents a registry where message handler can be registered.
    /// </summary>
    public interface IMessageHandlerRegistry
    {
        /// <summary>
        /// Registers a message handler.
        /// </summary>
        /// <param name="handlerRegistration">The message handler to register.</param>
        /// <returns>True, if the message handler was registered, false if a message handler of the specified type was already registered.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handlerRegistration"/> is null.</exception>
        bool Register(IMessageHandlerRegistration handlerRegistration);

        /// <summary>
        /// Unregisters a message handler.
        /// </summary>
        /// <param name="handlerRegistration">The message handler to unregister.</param>
        /// <returns>True, if the message handler was unregistered, false if a message handler of the specified type was not registered.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="handlerRegistration"/> is null.</exception>
        bool Unregister(IMessageHandlerRegistration handlerRegistration);

        /// <summary>
        /// Creates a <see cref="IMessageHandlerProvider"/> of the current snapshot of handler registrations.
        /// </summary>
        /// <returns>The created <see cref="IMessageHandlerProvider"/>.</returns>
        IMessageHandlerProvider ToProvider();
    }
}
