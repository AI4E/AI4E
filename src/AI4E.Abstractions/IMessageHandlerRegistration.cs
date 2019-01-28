/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IMessageHandler.cs
 * Types:           (1) AI4E.IMessageHandlerRegistration
 *                  (2) AI4E.IMessageHandlerRegistration'1
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

using System;

namespace AI4E
{
    /// <summary>
    /// Represents the registration of a message handler.
    /// </summary>
    public interface IMessageHandlerRegistration
    {
        /// <summary>
        /// Creates an instance of the registered message handler within the scope of the specified service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider that is used to obtain handler specific services.</param>
        /// <returns>The created instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is null.</exception>
        IMessageHandler CreateMessageHandler(IServiceProvider serviceProvider);

        /// <summary>
        /// Gets the type of message the registered handler is registered for.
        /// </summary>
        Type MessageType { get; }
    }

    /// <summary>
    /// Represents the registration of a message handler for the specified type of message.
    /// </summary>
    /// <typeparam name="TMessage">The type of message the handler is registered for.</typeparam>
    public interface IMessageHandlerRegistration<TMessage> : IMessageHandlerRegistration
            where TMessage : class
    {
        /// <summary>
        /// Creates an instance of the registered message handler within the scope of the specified service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider that is used to obtain handler specific services.</param>
        /// <returns>The created instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is null.</exception>
        new IMessageHandler<TMessage> CreateMessageHandler(IServiceProvider serviceProvider);
    }
}
