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
using System.Collections.Immutable;
using System.Globalization;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents the entry of a commit-attempt that describes the operation on a single entity.
    /// </summary>
    internal readonly struct CommitAttemptEntry : IEquatable<CommitAttemptEntry>, ICommitAttemptEntry
    {
        private readonly ITrackedEntity? _trackedEntity;
        private static readonly ImmutableHashSet<EntityTrackState> _allowedTrackStates = new[]
        {
            EntityTrackState.Created,
            EntityTrackState.Updated,
            EntityTrackState.Deleted
        }.ToImmutableHashSet();

        /// <summary>
        /// Creates a new instance of the <see cref="CommitAttemptEntry"/> type 
        /// from the specified <see cref="ITrackedEntity"/>.
        /// </summary>
        /// <param name="trackedEntity">The <see cref="ITrackedEntity"/> that tracks the entity to commit. </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="trackedEntity"/> is <c>null</c>.</exception>
        public CommitAttemptEntry(ITrackedEntity trackedEntity)
        {
            if (trackedEntity is null)
                throw new ArgumentNullException(nameof(trackedEntity));

            if (!_allowedTrackStates.Contains(trackedEntity.TrackState))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        Resources.TrackedEntityMustNotBeInState,
                        trackedEntity.TrackState), nameof(trackedEntity));
            }

            _trackedEntity = trackedEntity;
        }

        /// <inheritdoc/>
        public EntityIdentifier EntityIdentifier
            => _trackedEntity is null ? default : _trackedEntity.GetEntityIdentifier();

        /// <inheritdoc/>
        public CommitOperation Operation
        {
            get
            {
                if (_trackedEntity is null)
                    return CommitOperation.Delete;

                return _trackedEntity.TrackState switch
                {
                    EntityTrackState.Created => CommitOperation.Store,
                    EntityTrackState.Updated => CommitOperation.Store,
                    EntityTrackState.Deleted => CommitOperation.Delete,
                    _ => (CommitOperation)(-1)
                };
            }
        }

        /// <inheritdoc/>
        public long Revision => _trackedEntity is null ? default : _trackedEntity.Revision;

        /// <inheritdoc/>
        public ConcurrencyToken ConcurrencyToken
            => _trackedEntity is null ? default : _trackedEntity.ConcurrencyToken;

        /// <inheritdoc/>
        public DomainEventCollection DomainEvents
            => _trackedEntity is null ? default : _trackedEntity.DomainEvents;

        /// <inheritdoc/>
        public long ExpectedRevision
            => _trackedEntity is null ? default : _trackedEntity.OriginalEntityLoadResult.Revision;

        /// <inheritdoc/>
        public object? Entity => _trackedEntity is null ? default : _trackedEntity.Entity;

        bool IEquatable<CommitAttemptEntry>.Equals(CommitAttemptEntry other)
        {
            return Equals(in other);
        }

        /// <inheritdoc cref="IEquatable{CommitAttemptEntry}.Equals(CommitAttemptEntry)"/>
        public bool Equals(in CommitAttemptEntry other)
        {
            return _trackedEntity == other._trackedEntity;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is CommitAttemptEntry entry && Equals(in entry);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(_trackedEntity);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempt entries are equal.
        /// </summary>
        /// <param name="left">The first <see cref="CommitAttemptEntry"/>.</param>
        /// <param name="right">The second <see cref="CommitAttemptEntry"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in CommitAttemptEntry left, in CommitAttemptEntry right)
        {
            return left.Equals(in right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempt entries are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="CommitAttemptEntry"/>.</param>
        /// <param name="right">The second <see cref="CommitAttemptEntry"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in CommitAttemptEntry left, in CommitAttemptEntry right)
        {
            return !left.Equals(in right);
        }
    }
}
