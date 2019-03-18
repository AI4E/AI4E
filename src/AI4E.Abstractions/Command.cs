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
    /// A base type for command messages.
    /// </summary>
    /// <typeparam name="TId">The type of id the addressed resource uses.</typeparam>
    public abstract class Command<TId>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="Command{TId}"/> type.
        /// </summary>
        /// <param name="id">The id of the resource, the command addresses.</param>
        protected Command(TId id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets the id of the resource, the command addresses.
        /// </summary>
        public TId Id { get; }
    }

    /// <summary>
    /// A base type for command messages.
    /// </summary>
    public abstract class Command : Command<Guid>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="Command"/> type.
        /// </summary>
        /// <param name="id">The id of the resource, the command addresses.</param>
        protected Command(Guid id) : base(id) { }
    }

    /// <summary>
    /// A base type for command messages thats handlers check for conncurrency issues.
    /// </summary>
    /// <typeparam name="TId">The type of id the addressed resource uses.</typeparam>
    public abstract class ConcurrencySafeCommand<TId> : Command<TId>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ConcurrencySafeCommand{TId}"/> type.
        /// </summary>
        /// <param name="id">The id of the resource, the command addresses.</param>
        /// <param name="concurrencyToken">The concurrency token of the resource, the command addresses.</param>
        protected ConcurrencySafeCommand(TId id, string concurrencyToken) : base(id)
        {
            ConcurrencyToken = concurrencyToken;
        }

        /// <summary>
        /// Gets the concurrency token of the resource, the command addresses.
        /// </summary>
        public string ConcurrencyToken { get; }
    }

    /// <summary>
    /// A base type for command messages thats handlers check for conncurrency issues.
    /// </summary>
    public abstract class ConcurrencySafeCommand : ConcurrencySafeCommand<Guid>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ConcurrencySafeCommand{TId}"/> type.
        /// </summary>
        /// <param name="id">The id of the resource, the command addresses.</param>
        /// <param name="concurrencyToken">The concurrency token of the resource, the command addresses.</param>
        protected ConcurrencySafeCommand(Guid id, string concurrencyToken) : base(id, concurrencyToken) { }
    }
}
