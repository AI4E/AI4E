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
using System.Diagnostics.CodeAnalysis;

namespace AI4E.Storage.Domain
{
    public readonly struct CommitAttemptEntry : ICommitAttemptEntry, IEquatable<CommitAttemptEntry>
    {
        public CommitAttemptEntry(
            EntityIdentifier entityIdentifier,
            CommitOperation operation,
            long revision,
            ConcurrencyToken concurrencyToken,
            DomainEventCollection domainEvents,
            long expectedRevision,
            object? entity)
        {
            EntityIdentifier = entityIdentifier;
            Operation = operation;
            Revision = revision;
            ConcurrencyToken = concurrencyToken;
            DomainEvents = domainEvents;
            ExpectedRevision = expectedRevision;
            Entity = entity;
        }

        public EntityIdentifier EntityIdentifier { get; }

        public CommitOperation Operation { get; }

        public long Revision { get; }

        public ConcurrencyToken ConcurrencyToken { get; }

        public DomainEventCollection DomainEvents { get; }

        public long ExpectedRevision { get; }

        public object? Entity { get; }

        public bool Equals([AllowNull] CommitAttemptEntry other)
        {
            return Equals(in other);
        }

        public bool Equals([AllowNull] in CommitAttemptEntry other)
        {
            if (other.EntityIdentifier != EntityIdentifier)
                return false;

            if (other.Operation != Operation)
                return false;

            if (other.Revision != Revision)
                return false;

            if (other.ConcurrencyToken != ConcurrencyToken)
                return false;

            if (other.DomainEvents != DomainEvents)
                return false;

            if (other.ExpectedRevision != ExpectedRevision)
                return false;

            if (other.Entity != Entity)
                return false;

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is CommitAttemptEntry wrappingCommitAttemptEntry
                && Equals(in wrappingCommitAttemptEntry);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                EntityIdentifier, Operation, Revision, ConcurrencyToken, DomainEvents, ExpectedRevision, Entity);
        }

        public static bool operator ==(
            in CommitAttemptEntry left,
            in CommitAttemptEntry right)
        {
            return left.Equals(in right);
        }

        public static bool operator !=(
            in CommitAttemptEntry left,
            in CommitAttemptEntry right)
        {
            return !left.Equals(in right);
        }

        public static CommitAttemptEntryBuilder CreateBuilder()
        {
            return new CommitAttemptEntryBuilder();
        }

        public static CommitAttemptEntryBuilder CreateBuilder<TCommitAttemptEntry>(
            TCommitAttemptEntry commitAttemptEntry)
            where TCommitAttemptEntry : ICommitAttemptEntry
        {
            var result = CreateBuilder();
            result.FromEntry(commitAttemptEntry);
            return result;
        }

        // TODO: Change this to struct and cache the underlying reference type implementation
#pragma warning disable CA1034
        public sealed class CommitAttemptEntryBuilder : IDisposable
#pragma warning restore CA1034
        {
            internal CommitAttemptEntryBuilder() { }

            public EntityIdentifier EntityIdentifier { get; set; }

            public CommitOperation Operation { get; set; }

            public long Revision { get; set; }

            public ConcurrencyToken ConcurrencyToken { get; set; }

            public DomainEventCollection DomainEvents { get; set; }

            public long ExpectedRevision { get; set; }

            public object? Entity { get; set; }

            public void FromEntry<TCommitAttemptEntry>(TCommitAttemptEntry commitAttemptEntry)
                where TCommitAttemptEntry : ICommitAttemptEntry
            {
                EntityIdentifier = commitAttemptEntry.EntityIdentifier;
                Operation = commitAttemptEntry.Operation;
                Revision = commitAttemptEntry.Revision;
                ConcurrencyToken = commitAttemptEntry.ConcurrencyToken;
                DomainEvents = commitAttemptEntry.DomainEvents;
                ExpectedRevision = commitAttemptEntry.ExpectedRevision;
                Entity = commitAttemptEntry.Entity;
            }

            public void Build(out CommitAttemptEntry commitAttemptEntry)
            {
                commitAttemptEntry = new CommitAttemptEntry(
                    EntityIdentifier,
                    Operation,
                    Revision,
                    ConcurrencyToken,
                    DomainEvents,
                    ExpectedRevision,
                    Entity);
            }

            public void Dispose() { }
        }
    }
}
