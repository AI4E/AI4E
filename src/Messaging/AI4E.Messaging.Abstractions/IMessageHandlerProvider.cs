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

namespace AI4E.Messaging
{
    /// <summary>
    /// Represents a provider of message handlers.
    /// </summary>
    public interface IMessageHandlerProvider
    {
        /// <summary>
        /// Returns an ordered collection of message handler registrations for the specified message type.
        /// </summary>
        /// <param name="messageType">The message type.</param>
        /// <returns>An ordered collection of message handler registrations for <paramref name="messageType"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageType"/> is <c>null</c>.</exception>
        IReadOnlyList<IMessageHandlerRegistration> GetHandlerRegistrations(Type messageType);

        /// <summary>
        /// Returns an ordered collection of all message handler registrations.
        /// </summary>
        /// <returns>An ordered collection of message handler registrations.</returns>
        IReadOnlyList<IMessageHandlerRegistration> GetHandlerRegistrations();
    }
}
