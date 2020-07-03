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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain.Tracking
{
    /// <inheritdoc cref="IUnitOfWork{TLoadResult}"/>
    public sealed class UnitOfWork<TLoadResult> : IUnitOfWork<TLoadResult>
        where TLoadResult : class, IEntityLoadResult
    {
        private readonly IEntityConcurrencyTokenFactory _concurrencyTokenFactory;
        private readonly Dictionary<EntityIdentifier, UnitOfWorkEntry<TLoadResult>> _entries;

        /// <summary>
        /// Creates a new instance of the <see cref="UnitOfWork{TLoadResult}"/> type.
        /// </summary>
        /// <param name="concurrencyTokenFactory">
        /// The concurrency-token factory used to create concurrency-tokens.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="concurrencyTokenFactory"/> is <c>null</c>.
        /// </exception>
        public UnitOfWork(IEntityConcurrencyTokenFactory concurrencyTokenFactory)
        {
            if (concurrencyTokenFactory is null)
                throw new ArgumentNullException(nameof(concurrencyTokenFactory));

            _entries = new Dictionary<EntityIdentifier, UnitOfWorkEntry<TLoadResult>>();
            _concurrencyTokenFactory = concurrencyTokenFactory;
        }

        // TODO: Add a UnitOfWor2EntryCollection struct that implements IReadOnlyCollectiony<UnitOfWorkEntry>,
        //       in order that we do not have to precompute the list of all tracked entities.

        /// <inheritdoc cref="IUnitOfWork{TLoadResult}.Entries"/>
        public IReadOnlyList<UnitOfWorkEntry<TLoadResult>> Entries => _entries.Values.ToImmutableList();

        IReadOnlyList<IUnitOfWorkEntry<TLoadResult>> IUnitOfWork<TLoadResult>.Entries => Entries;

        /// <inheritdoc cref="IUnitOfWork{TLoadResult}.TryGetEntry(EntityIdentifier, out IUnitOfWorkEntry{TLoadResult}?)"/>
        public bool TryGetEntry(
            EntityIdentifier entityIdentifier, [NotNullWhen(true)] out UnitOfWorkEntry<TLoadResult>? entry)
        {
            if (_entries.TryGetValue(entityIdentifier, out entry))
            {
                return true;
            }

            entry = null;
            return false;
        }

        bool IUnitOfWork<TLoadResult>.TryGetEntry(
            EntityIdentifier entityIdentifier, [NotNullWhen(true)] out IUnitOfWorkEntry<TLoadResult>? entry)
        {
            entry = null;
            return TryGetEntry(
                entityIdentifier, 
                out Unsafe.As<IUnitOfWorkEntry<TLoadResult>, UnitOfWorkEntry<TLoadResult>>(ref entry!)!);
        }

        /// <inheritdoc cref="IUnitOfWork{TLoadResult}.GetOrUpdate(ITrackableEntityLoadResult{TLoadResult})"/>
        public UnitOfWorkEntry<TLoadResult> GetOrUpdate(ITrackableEntityLoadResult<TLoadResult> entityLoadResult)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            var entityIdentifier = entityLoadResult.EntityIdentifier;

            if (!_entries.TryGetValue(entityIdentifier, out var entry))
            {
                entry = new UnitOfWorkEntry<TLoadResult>(this, entityLoadResult.AsTracked(_concurrencyTokenFactory));
                _entries[entityIdentifier] = entry;
            }

            return entry;
        }

        IUnitOfWorkEntry<TLoadResult> IUnitOfWork<TLoadResult>.GetOrUpdate(
            ITrackableEntityLoadResult<TLoadResult> entityLoadResult)
        {
            return GetOrUpdate(entityLoadResult);
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _entries.Clear();
        }

        /// <inheritdoc/>
        public ValueTask<EntityCommitResult> CommitAsync(
            IEntityStorageEngine storageEngine,
            CancellationToken cancellation = default)
        {
            if (storageEngine is null)
                throw new ArgumentNullException(nameof(storageEngine));

            // Create a commit attempt of our tracked changed before we rollback.
            var commitAttempt = CreateCommitAttempt();

            // We rollback in any case.
            Reset();

            return storageEngine.CommitAsync(commitAttempt, cancellation);
        }

        private CommitAttempt<CommitAttemptEntry<TLoadResult>> CreateCommitAttempt()
        {
            var commitAttemptEntriesBuilder = ImmutableArray.CreateBuilder<CommitAttemptEntry<TLoadResult>>(
                initialCapacity: _entries.Count);

            var i = 0;
            foreach (var entry in _entries.Values)
            {
                if (!entry.IsModified)
                    continue;

                ref var commitAttemptEntry = ref Unsafe.AsRef(in commitAttemptEntriesBuilder.ItemRef(i++));
                commitAttemptEntry = new CommitAttemptEntry<TLoadResult>(entry); // TODO: Unscope 
            }

            var commitAttemptEntries = new CommitAttemptEntryCollection<CommitAttemptEntry<TLoadResult>>(
                commitAttemptEntriesBuilder.ToImmutable());

            return new CommitAttempt<CommitAttemptEntry<TLoadResult>>(commitAttemptEntries);
        }

        internal void UpdateEntry(UnitOfWorkEntry<TLoadResult> entry)
        {
            _entries[entry.EntityLoadResult.EntityIdentifier] = entry;
        }
    }
}
