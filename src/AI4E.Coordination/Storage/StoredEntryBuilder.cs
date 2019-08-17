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
using System.Collections.Immutable;
using System.Linq;
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Storage
{
    public sealed class StoredEntryBuilder : IStoredEntryBuilder
    {
        private static readonly ImmutableArray<SessionIdentifier> _noReadLocks = ImmutableArray<SessionIdentifier>.Empty;
        private readonly SessionIdentifier _session;

        public StoredEntryBuilder(IStoredEntry entry, SessionIdentifier session)
        {
            _session = session;

            Key = entry.Key;
            Value = entry.Value;
            ReadLocks = entry.ReadLocks.IsDefaultOrEmpty ? _noReadLocks : entry.ReadLocks;
            WriteLock = entry.WriteLock;
            StorageVersion = entry.StorageVersion;
            IsMarkedAsDeleted = entry.IsMarkedAsDeleted;
        }

        public StoredEntryBuilder(string key, SessionIdentifier session)
        {
            _session = session;

            Key = key;
            ReadLocks = _noReadLocks;
            IsMarkedAsDeleted = true;
        }

        public string Key { get; }

        public ReadOnlyMemory<byte> Value { get; private set; }

        public ImmutableArray<SessionIdentifier> ReadLocks { get; private set; }

        public SessionIdentifier? WriteLock { get; private set; }

        public int StorageVersion { get; private set; }

        public bool IsMarkedAsDeleted { get; private set; }

        public void Create(ReadOnlyMemory<byte> value)
        {
            if (!IsMarkedAsDeleted)
                throw new InvalidOperationException("The entry is alread present.");

            if (ReadLocks.Any() && ReadLocks.Any(p => p != _session) || WriteLock != null && WriteLock != _session)
                throw new InvalidOperationException("The entry is not free of locks.");

            Value = value;

            if (!ReadLocks.Contains(_session))
                ReadLocks = ReadLocks.Add(_session);

            WriteLock = _session;
            StorageVersion = 1;
            IsMarkedAsDeleted = false;
            ChangesPending = true;
        }

        public void AcquireWriteLock()
        {
            if (WriteLock == _session)
                return;

            if (WriteLock != null)
                throw new InvalidOperationException("The lock is not free.");

            WriteLock = _session;
            StorageVersion++;
            ChangesPending = true;
        }

        public void ReleaseWriteLock()
        {
            if (WriteLock == null)
                return;

            if (WriteLock != _session)
                throw new InvalidOperationException();

            // We cannot check this here, as this is called when the session that holds write-lock is terminated too.
            // In this case, there may be read-locks present.
            //if (ReadLocks.Length != 0 && (ReadLocks.Length > 1 || ReadLocks.First() != WriteLock))       
            //    throw new InvalidOperationException();

            WriteLock = null;
            StorageVersion++;
            ChangesPending = true;
        }

        public void AcquireReadLock()
        {
            if (WriteLock != null && WriteLock != _session)
                throw new InvalidOperationException();

            if (ReadLocks.Contains(_session))
                return;

            if (IsMarkedAsDeleted)
                throw new InvalidOperationException("The entry is invalidated.");

            ReadLocks = ReadLocks.Add(_session);
            StorageVersion++;
            ChangesPending = true;
        }

        public void ReleaseReadLock()
        {
            if (ReadLocks.Length == 0 || !ReadLocks.Contains(_session))
                return;

            ReadLocks = ReadLocks.Remove(_session);
            StorageVersion++;
            ChangesPending = true;
        }

        public void MarkAsDeleted()
        {
            if (ReadLocks.Any() && ReadLocks.Any(p => p != _session))
                throw new InvalidOperationException();

            if (WriteLock != _session)
                throw new InvalidOperationException();

            if (IsMarkedAsDeleted)
                return;

            IsMarkedAsDeleted = true;
            StorageVersion++;
            ChangesPending = true;
        }

        public void SetValue(ReadOnlyMemory<byte> value)
        {
            if (ReadLocks.Any() && ReadLocks.Any(p => p != _session))
                throw new InvalidOperationException();

            if (WriteLock != _session)
                throw new InvalidOperationException();

            if (IsMarkedAsDeleted)
                throw new InvalidOperationException();

            Value = value;
            StorageVersion++;
            ChangesPending = true;
        }

        public bool ChangesPending { get; private set; }

        public IStoredEntry ToImmutable(bool reset)
        {
            if (reset)
                ChangesPending = false;

            if (IsMarkedAsDeleted && WriteLock == null && !ReadLocks.Any())
            {
                return null;
            }

            return new StoredEntry(Key, Value, ReadLocks, WriteLock, StorageVersion, IsMarkedAsDeleted);
        }

        private sealed class StoredEntry : IStoredEntry
        {
            public StoredEntry(
                string key,
                ReadOnlyMemory<byte> value,
                ImmutableArray<SessionIdentifier> readLocks,
                SessionIdentifier? writeLock,
                int storageVersion,
                bool isMarkedAsDeleted)
            {
                Key = key;
                Value = value;
                ReadLocks = readLocks;
                WriteLock = writeLock;
                StorageVersion = storageVersion;
                IsMarkedAsDeleted = isMarkedAsDeleted;
            }

            public string Key { get; }

            public ReadOnlyMemory<byte> Value { get; }

            public ImmutableArray<SessionIdentifier> ReadLocks { get; }

            public SessionIdentifier? WriteLock { get; }

            public int StorageVersion { get; }

            public bool IsMarkedAsDeleted { get; }
        }
    }

    public static class StoredEntryExtension
    {
        public static IStoredEntryBuilder ToBuilder(this IStoredEntry storedEntry, SessionIdentifier session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            return new StoredEntryBuilder(storedEntry, session);
        }
    }
}
