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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IUnitOfWork"/>
    internal sealed partial class UnitOfWork : IUnitOfWork
    {
        private readonly IEntityConcurrencyTokenFactory _concurrencyTokenFactory;
        private readonly Dictionary<EntityIdentifier, ITrackedEntity> _trackedEntities;

        // Some book-keeping for performance optimization.
        private int _numberOfUntrackedEntries = 0;
        private int _numberOfModifiedEntries = 0;

        /// <summary>
        /// Creates a new instance of the <see cref="UnitOfWork"/> type.
        /// </summary>
        /// <param name="concurrencyTokenFactory">
        /// The <see cref="IEntityConcurrencyTokenFactory"/> used to create concurrency-tokens.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="concurrencyTokenFactory"/> is <c>null</c>.
        /// </exception>
        public UnitOfWork(IEntityConcurrencyTokenFactory concurrencyTokenFactory)
        {
            if (concurrencyTokenFactory is null)
                throw new ArgumentNullException(nameof(concurrencyTokenFactory));

            _concurrencyTokenFactory = concurrencyTokenFactory;
            _trackedEntities = new Dictionary<EntityIdentifier, ITrackedEntity>();
        }

        /// <inheritdoc/>
        public IReadOnlyList<ITrackedEntity> TrackedEntities
        {
            get
            {
                var result = new List<ITrackedEntity>(capacity: _trackedEntities.Count - _numberOfUntrackedEntries);

                foreach (var trackedEntity in _trackedEntities.Values)
                {
                    if (trackedEntity.TrackState == EntityTrackState.Untracked)
                        continue;

                    result.Add(trackedEntity);
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<ITrackedEntity> ModifiedEntities
        {
            get
            {
                var result = new List<ITrackedEntity>(capacity: _numberOfModifiedEntries);

                foreach (var trackedEntity in _trackedEntities.Values)
                {
                    if (!trackedEntity.IsModified())
                        continue;

                    result.Add(trackedEntity);
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public bool TryGetTrackedEntity(
            EntityIdentifier entityIdentifier,
            [NotNullWhen(true)] out ITrackedEntity? trackedEntity)
        {

            if (_trackedEntities.TryGetValue(entityIdentifier, out trackedEntity)
                && trackedEntity.TrackState != EntityTrackState.Untracked)
            {
                return true;
            }

            trackedEntity = null;
            return false;
        }

        /// <inheritdoc/>
        public ITrackedEntity GetOrUpdate(ICacheableEntityLoadResult entityLoadResult)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            var entityIdentifier = entityLoadResult.EntityIdentifier;
            ConcurrencyToken? concurrencyToken = null;
            long? revision = null;
            DomainEventCollection domainEvents = default;

            if (_trackedEntities.TryGetValue(entityIdentifier, out var trackedEntity))
            {
                if (trackedEntity.TrackState != EntityTrackState.Untracked)
                {
                    return trackedEntity;
                }

                concurrencyToken = trackedEntity.UpdatedConcurrencyToken;
                revision = trackedEntity.UpdatedRevision;
                domainEvents = trackedEntity.DomainEvents;
                _numberOfUntrackedEntries -= 1;
            }

            if (entityLoadResult.IsSuccess())
            {
                var successLoadResult = entityLoadResult.AsSuccessLoadResult().AsCachedResult();
                trackedEntity = TrackedEntity.UnchangedCacheEntry(this, successLoadResult, concurrencyToken, revision, domainEvents);
            }
            else
            {
                trackedEntity = TrackedEntity.NonExistentCacheEntry(this, entityLoadResult.EntityIdentifier, concurrencyToken, revision, domainEvents);
            }

            _trackedEntities[entityIdentifier] = trackedEntity;
            return trackedEntity;
        }

        private void Update(TrackedEntity trackedEntity)
        {
            if (_trackedEntities.TryGetValue(trackedEntity.GetEntityIdentifier(), out var currentEntry))
            {
                if (currentEntry.TrackState == EntityTrackState.Untracked)
                {
                    _numberOfUntrackedEntries -= 1;
                }
                else if (currentEntry.IsModified())
                {
                    _numberOfModifiedEntries -= 1;
                }
            }

            _trackedEntities[trackedEntity.GetEntityIdentifier()] = trackedEntity;

            if (trackedEntity.TrackState == EntityTrackState.Untracked)
            {
                _numberOfUntrackedEntries += 1;
            }
            else if (trackedEntity.IsModified())
            {
                _numberOfModifiedEntries += 1;
            }
        }

        private ConcurrencyToken CreateConcurrencyToken(EntityIdentifier entityIdentifier)
        {
            return _concurrencyTokenFactory.CreateConcurrencyToken(entityIdentifier);
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _trackedEntities.Clear();
            _numberOfUntrackedEntries = 0;
            _numberOfModifiedEntries = 0;
        }

        /// <inheritdoc/>
        public ValueTask<EntityCommitResult> CommitAsync(
            IEntityStorageEngine storageEngine,
            CancellationToken cancellation)
        {
            if (storageEngine is null)
                throw new ArgumentNullException(nameof(storageEngine));

            // Create a commit attempt of our tracked changed before we rollback.
            var commitAttempt = CreateCommitAttempt();

            // We rollback in any case.
            Reset();

            return storageEngine.CommitAsync(commitAttempt, cancellation);
        }

        private CommitAttempt<CommitAttemptEntry> CreateCommitAttempt()
        {
            var trackedEntities = ModifiedEntities;
            var commitAttemptEntriesBuilder = ImmutableArray.CreateBuilder<CommitAttemptEntry>(
                initialCapacity: trackedEntities.Count);
            commitAttemptEntriesBuilder.Count = trackedEntities.Count;

            for (var i = 0; i < trackedEntities.Count; i++)
            {
                ref var entry = ref Unsafe.AsRef(in commitAttemptEntriesBuilder.ItemRef(i));
                entry = new CommitAttemptEntry(trackedEntities[i]);
            }

            Debug.Assert(commitAttemptEntriesBuilder.Capacity == commitAttemptEntriesBuilder.Count);

            var commitAttemptEntries = new CommitAttemptEntryCollection<CommitAttemptEntry>(
                commitAttemptEntriesBuilder.MoveToImmutable());

            return new CommitAttempt<CommitAttemptEntry>(commitAttemptEntries);
        }
    }
}
