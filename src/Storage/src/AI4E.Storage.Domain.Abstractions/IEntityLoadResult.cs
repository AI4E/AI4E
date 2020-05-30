/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents the result of an entity load operation.
    /// </summary>
    public interface IEntityLoadResult
    {
        /// <summary>
        /// Gets the <see cref="EntityIdentifier"/> of the entity that a load operation was performed for.
        /// </summary>
        EntityIdentifier EntityIdentifier { get; }

        /// <summary>
        /// Gets the concurrency-token of the loaded entity or a default value if not available.
        /// </summary>
        /// <remarks>
        /// This is guaranteed to be available only in case that the load operation was successful.
        /// </remarks>
        ConcurrencyToken ConcurrencyToken { get; }

        /// <summary>
        /// Gets the revision of the loaded entity or a default value if not available.
        /// </summary>
        /// <remarks>
        /// This is guaranteed to be available only in case that the load operation was successful.
        /// </remarks>
        long Revision { get; }

        // TODO: Do we include the history of all domain-events?

        /// <summary>
        /// Gets the reason phrase that indicates the reason of the load-result state or a failure message.
        /// </summary>
        string Reason { get; }
    }
}