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
    /// A base type for event notifications.
    /// </summary>
    /// <typeparam name="TId">The type of id the resource uses that caused the event.</typeparam>
    public abstract class Event<TId>
    {
        /// <summary>
        /// Creates an instance of the <see cref="Event"/> type.
        /// </summary>
        /// <param name="id">The id of the resource that caused the event.</param>
        public Event(TId id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets the id of the resource that caused the event.
        /// </summary>
        public TId Id { get; }
    }

    /// <summary>
    /// A base type for event notifications.
    /// </summary>
    public abstract class Event : Event<Guid>
    {
        /// <summary>
        /// Creates an instance of the <see cref="Event"/> type.
        /// </summary>
        /// <param name="id">The id of the resource that caused the event.</param>
        public Event(Guid id) : base(id) { }
    }
}
