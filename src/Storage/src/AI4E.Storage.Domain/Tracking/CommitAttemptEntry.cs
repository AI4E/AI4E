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

using System;

namespace AI4E.Storage.Domain.Tracking
{
    /// <summary>
    /// Represents the entry of a commit-attempt that describes the operation on a single entity.
    /// </summary>
    /// /// <typeparam name="TLoadResult">The type of track-able load-result.</typeparam>
    internal readonly struct CommitAttemptEntry<TLoadResult> 
        : IEquatable<CommitAttemptEntry<TLoadResult>>, ICommitAttemptEntry
         where TLoadResult : class, IEntityLoadResult
    {
        private readonly IUnitOfWorkEntry<TLoadResult>? _unitOfWorkEntry;

        /// <summary>
        /// Creates a new instance of the <see cref="CommitAttemptEntry{TLoadResult}"/> type 
        /// from the specified <see cref="IUnitOfWorkEntry{TLoadResult}"/>.
        /// </summary>
        /// <param name="unitOfWorkEntry">
        /// The <see cref="IUnitOfWorkEntry{TLoadResult}"/> that tracks the entity to commit.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="unitOfWorkEntry"/> is <c>null</c>.
        /// </exception>
        public CommitAttemptEntry(IUnitOfWorkEntry<TLoadResult> unitOfWorkEntry)
        {
            if (unitOfWorkEntry is null)
                throw new ArgumentNullException(nameof(unitOfWorkEntry));

            _unitOfWorkEntry = unitOfWorkEntry;
        }

        /// <inheritdoc/>
        public EntityIdentifier EntityIdentifier
            => _unitOfWorkEntry is null ? default : _unitOfWorkEntry.EntityLoadResult.EntityIdentifier;

        /// <inheritdoc/>
        public CommitOperation Operation
        {
            get
            {
                if (_unitOfWorkEntry is null)
                {
                    return CommitOperation.AppendEventsOnly;
                }

                var originalFound = _unitOfWorkEntry.EntityLoadResult.TrackedLoadResult.IsFound();
                var updatedFound = _unitOfWorkEntry.EntityLoadResult.IsFound();

                // Created or updated
                if (updatedFound)
                {
                    return CommitOperation.Store;
                }

                if (originalFound)
                {
                    return CommitOperation.Delete;
                }

                return CommitOperation.AppendEventsOnly;
            }
        }

        /// <inheritdoc/>
        public long Revision => _unitOfWorkEntry is null ? default : _unitOfWorkEntry.EntityLoadResult.Revision;

        /// <inheritdoc/>
        public ConcurrencyToken ConcurrencyToken
            => _unitOfWorkEntry is null ? default : _unitOfWorkEntry.EntityLoadResult.ConcurrencyToken;

        /// <inheritdoc/>
        public DomainEventCollection DomainEvents
            => _unitOfWorkEntry is null ? default : _unitOfWorkEntry.RecordedDomainEvents;

        /// <inheritdoc/>
        public long ExpectedRevision
            => _unitOfWorkEntry is null ? default : _unitOfWorkEntry.EntityLoadResult.TrackedLoadResult.Revision;

        /// <inheritdoc/>
        public object? Entity
        {
            get
            {
                if (_unitOfWorkEntry is null)
                    return null;

                var entityLoadResult = (IEntityLoadResult)_unitOfWorkEntry.EntityLoadResult;

                // Unscope the load-result (= scope to the global scope)
                if(entityLoadResult.IsScopeable<IEntityQueryResult>(out var scopeableEntityQueryResult))
                {
                    entityLoadResult = scopeableEntityQueryResult.AsScopedTo(EntityQueryResultGlobalScope.Instance);
                }

                if (entityLoadResult.IsFound(out var foundEntityQueryResult))
                {
                    return foundEntityQueryResult.Entity;
                }

                return null;
            }
        }

        /// <inheritdoc/>
        public bool Equals(CommitAttemptEntry<TLoadResult> other)
        {
            return _unitOfWorkEntry == other._unitOfWorkEntry;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is CommitAttemptEntry<TLoadResult> entry && Equals(entry);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(_unitOfWorkEntry);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempt entries are equal.
        /// </summary>
        /// <param name="left">The first <see cref="CommitAttemptEntry{TLoadResult}"/>.</param>
        /// <param name="right">The second <see cref="CommitAttemptEntry{TLoadResult}"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(CommitAttemptEntry<TLoadResult> left, CommitAttemptEntry<TLoadResult> right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempt entries are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="CommitAttemptEntry{TLoadResult}"/>.</param>
        /// <param name="right">The second <see cref="CommitAttemptEntry{TLoadResult}"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(CommitAttemptEntry<TLoadResult> left, CommitAttemptEntry<TLoadResult> right)
        {
            return !left.Equals(right);
        }
    }
}
