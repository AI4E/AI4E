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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a commit-attempt.
    /// </summary>
    /// <typeparam name="TCommitAttemptEntry">The type of commit-attempt entry.</typeparam>
    public readonly struct CommitAttempt<TCommitAttemptEntry> : IEquatable<CommitAttempt<TCommitAttemptEntry>>
         where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
    {
        /// <summary>
        /// Creates a new instance of type <see cref="CommitAttempt{TCommitAttemptEntry}"/>.
        /// </summary>
        /// <param name="unitOfWork">
        /// The <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/> that defines the commit entries that 
        /// the commit-attempt contains of.
        /// </param>
        public CommitAttempt(CommitAttemptEntryCollection<TCommitAttemptEntry> entries)
        {
            Entries = entries;
        }

        /// <summary>
        /// Gets the <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/> that defines the commit entries 
        /// that the current commit-attempt contains of.
        /// </summary>
        public CommitAttemptEntryCollection<TCommitAttemptEntry> Entries { get; }

        bool IEquatable<CommitAttempt<TCommitAttemptEntry>>.Equals(CommitAttempt<TCommitAttemptEntry> other)
        {
            return Equals(in other);
        }

        /// <inheritdoc cref="IEquatable{CommitAttempt}.Equals(CommitAttempt{TCommitAttemptEntry})"/>
        public bool Equals(in CommitAttempt<TCommitAttemptEntry> other)
        {
            return Entries == other.Entries;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is CommitAttempt<TCommitAttemptEntry> commitAttempt && Equals(in commitAttempt);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Entries.GetHashCode();
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempts are equal.
        /// </summary>
        /// <param name="left">The first <see cref="CommitAttempt"/>.</param>
        /// <param name="right">The second <see cref="CommitAttempt"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(
            in CommitAttempt<TCommitAttemptEntry> left, 
            in CommitAttempt<TCommitAttemptEntry> right)
        {
            return left.Equals(in right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempts are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="CommitAttempt"/>.</param>
        /// <param name="right">The second <see cref="CommitAttempt"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(
            in CommitAttempt<TCommitAttemptEntry> left, 
            in CommitAttempt<TCommitAttemptEntry> right)
        {
            return !left.Equals(in right);
        }
    }

    /// <summary>
    /// Represents the entry of a commit-attempt that describes the operation on a single entity.
    /// </summary>
    public readonly struct CommitAttemptEntry : IEquatable<CommitAttemptEntry>, ICommitAttemptEntry
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

    /// <summary>
    /// Represents an immutable collection of commit-attempt entries.
    /// </summary>
    /// <typeparam name="TCommitAttemptEntry">The type of commit-attempt entry.</typeparam>
    public readonly struct CommitAttemptEntryCollection<TCommitAttemptEntry>
        : IReadOnlyCollection<TCommitAttemptEntry>, IEquatable<CommitAttemptEntryCollection<TCommitAttemptEntry>>
        where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
    {
        private readonly ImmutableArray<TCommitAttemptEntry> _entries;

        /// <summary>
        /// Creates a new instance of type <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/> 
        /// from the specified collection of commit-attempt entries.
        /// </summary>
        /// <param name="entries">
        /// An <see cref="ImmutableArray{TCommitAttemptEntry}"/> containing the commit-attempt entries.
        /// </param>
        public CommitAttemptEntryCollection(ImmutableArray<TCommitAttemptEntry> entries)
        {
            _entries = entries;
        }

        /// <inheritdoc/>
        public int Count => _entries.IsDefaultOrEmpty ? 0 : _entries.Length;

        bool IEquatable<CommitAttemptEntryCollection<TCommitAttemptEntry>>.Equals(
            CommitAttemptEntryCollection<TCommitAttemptEntry> other)
        {
            return Equals(in other);
        }

        /// <inheritdoc cref="IEquatable{CommitAttemptEntryCollection}.Equals(CommitAttemptEntryCollection)"/>
        public bool Equals(in CommitAttemptEntryCollection<TCommitAttemptEntry> other)
        {
            if (other.Count != Count)
                return false;

            for (var i = 0; i < Count; i++)
            {
                ref readonly var left = ref _entries.ItemRef(i);
                ref readonly var right = ref other._entries.ItemRef(i);

                if (!left.Equals(right))
                    return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is CommitAttemptEntryCollection<TCommitAttemptEntry> commitAttemptEntries 
                && Equals(in commitAttemptEntries);
        }

        private const int _sequenceHashCodeSeedValue = 0x2D2816FE;
        private const int _sequenceHashCodePrimeNumber = 397;

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (Count == 0)
                return 0;

            var result = _sequenceHashCodeSeedValue;

            foreach (var entry in _entries)
            {
                result = result * _sequenceHashCodePrimeNumber + entry.GetHashCode();
            }

            return result;
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempt entry collections are equal.
        /// </summary>
        /// <param name="left">The first <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/>.</param>
        /// <param name="right">The second <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(
            in CommitAttemptEntryCollection<TCommitAttemptEntry> left, 
            in CommitAttemptEntryCollection<TCommitAttemptEntry> right)
        {
            return left.Equals(in right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempt entry collections are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/>.</param>
        /// <param name="right">The second <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(
            in CommitAttemptEntryCollection<TCommitAttemptEntry> left, 
            in CommitAttemptEntryCollection<TCommitAttemptEntry> right)
        {
            return !left.Equals(in right);
        }

        IEnumerator<TCommitAttemptEntry> IEnumerable<TCommitAttemptEntry>.GetEnumerator()
        {
            if (_entries.IsDefaultOrEmpty)
                return Enumerable.Empty<TCommitAttemptEntry>().GetEnumerator();

            return ((IEnumerable<TCommitAttemptEntry>)_entries).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (_entries.IsDefaultOrEmpty)
                return Enumerable.Empty<TCommitAttemptEntry>().GetEnumerator();

            return ((IEnumerable)_entries).GetEnumerator();
        }

        /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// Represents an enumerator that enumerator through a 
        /// <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TCommitAttemptEntry>, IEnumerator
        {
            // This MUST NOT be marked read-only, to allow the compiler to access this field by reference.
            private ImmutableArray<TCommitAttemptEntry>.Enumerator _underlying;

            /// <summary>
            /// Creates a new instance of the <see cref="Enumerator"/> type enumerating 
            /// the specified <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/>.
            /// </summary>
            /// <param name="collection">
            /// The <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/> to enumerate.
            /// </param>
            public Enumerator(CommitAttemptEntryCollection<TCommitAttemptEntry> collection)
            {
                if (collection._entries.IsDefault)
                {
                    _underlying = ImmutableArray<TCommitAttemptEntry>.Empty.GetEnumerator();
                }
                else
                {
                    _underlying = collection._entries.GetEnumerator();
                }
            }

            /// <inheritdoc/>
            public TCommitAttemptEntry Current => _underlying.Current;

            [ExcludeFromCodeCoverage]
            object IEnumerator.Current => Current;

            /// <inheritdoc/>
            public bool MoveNext()
            {
                return _underlying.MoveNext();
            }

            /// <inheritdoc/>
            public void Dispose() { }

            [ExcludeFromCodeCoverage]
            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }
        }
    }

    /// <summary>
    /// Defines constants for possible commit operations.
    /// </summary>
    public enum CommitOperation
    {
        /// <summary>
        /// An entity shall be created or updated.
        /// </summary>
        Store = 0,

        /// <summary>
        /// An entity shall be deleted.
        /// </summary>
        Delete
    }
}
