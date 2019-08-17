/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Storage
{
    public interface IStoredEntryBuilder
    {
        /// <summary>
        /// Gets the key of the entry.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the value of the entry.
        /// </summary>
        ReadOnlyMemory<byte> Value { get; }

        /// <summary>
        /// Gets a collection of session that read locks are held for.
        /// </summary>
        ImmutableArray<SessionIdentifier> ReadLocks { get; }

        /// <summary>
        /// Gets the session that a write lock is held for.
        /// </summary>
        SessionIdentifier? WriteLock { get; }

        int StorageVersion { get; }

        bool IsMarkedAsDeleted { get; }

        void Create(ReadOnlyMemory<byte> value);

        void AcquireWriteLock();

        void ReleaseWriteLock();

        void AcquireReadLock();

        void ReleaseReadLock();

        void MarkAsDeleted();

        void SetValue(ReadOnlyMemory<byte> value);

        bool ChangesPending { get; }

        IStoredEntry ToImmutable(bool reset = true);
    }
}
