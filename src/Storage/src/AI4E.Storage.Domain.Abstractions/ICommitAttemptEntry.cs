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
    /// Represents the entry of a commit-attempt that describes the operation on a single entity.
    /// </summary>
    public interface ICommitAttemptEntry
    {
        /// <summary>
        /// Gets the identifier of the entity to commit.
        /// </summary>
        EntityIdentifier EntityIdentifier { get; }

        /// <summary>
        /// Gets the commit operation to perform for the entity to commit.
        /// </summary>
        CommitOperation Operation { get; }

        /// <summary>
        /// Gets the new revision of the entity after performing the commit.
        /// </summary>
        long Revision { get; }

        /// <summary>
        /// Gets the concurrency-token of the entity after performing the commit.
        /// </summary>
        ConcurrencyToken ConcurrencyToken { get; }

        /// <summary>
        /// Gets the collection of domain-events that were raised on the entity.
        /// </summary>
        DomainEventCollection DomainEvents { get; }

        /// <summary>
        /// Gets the expected revision of the entity to commit to check for concurrency situations.
        /// </summary>
        long ExpectedRevision { get; }

        /// <summary>
        /// Gets the updated or created entity or <c>null</c> if a delete operation shall be performed.
        /// </summary>
        object? Entity { get; }
    }
}