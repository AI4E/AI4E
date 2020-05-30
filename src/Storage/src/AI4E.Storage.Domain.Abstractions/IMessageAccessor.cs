/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Accesses the content of messages.
    /// </summary>
    public interface IMessageAccessor
    {
        /// <summary>
        /// Tries to read the entity id from the specified message.
        /// </summary>
        /// <typeparam name="TMessage">The type of message.</typeparam>
        /// <param name="message">The message to read the id from.</param>
        /// <param name="id">
        /// Contains the id if the operation is successful and the message contains a non-null entity id.
        /// </param>
        /// <returns>True if the entity id was successfully read from the message.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        /// <remarks>
        /// If the operation returns <c>true</c> this does not necessarily imply, 
        /// that <paramref name="id"/> is non-null, as the message can contain a null id entry.
        /// </remarks>
        bool TryGetEntityId<TMessage>(TMessage message, out string? id) where TMessage : class;

        /// <summary>
        /// Reads the entity concurrency-token from the specified message.
        /// </summary>
        /// <typeparam name="TMessage">The type of message.</typeparam>
        /// <param name="message">The message to read the concurrency-token from.</param>
        /// <returns>
        /// The concurrency-token if the operation is successful and the message contains a non-null entity 
        /// concurrency-token, a default concurrency-token otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="message"/> is <c>null</c>.
        /// </exception>
        ConcurrencyToken GetConcurrencyToken<TMessage>(TMessage message) where TMessage : class;
    }
}
