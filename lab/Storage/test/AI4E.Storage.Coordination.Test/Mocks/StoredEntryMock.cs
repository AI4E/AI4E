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
using AI4E.Storage.Coordination.Session;
using AI4E.Storage.Coordination.Storage;

namespace AI4E.Storage.Coordination.Mocks
{
    public sealed class StoredEntryMock : IStoredEntry
    {
        private ImmutableArray<SessionIdentifier> _readLocks;

        public string Key { get; set; }

        public ReadOnlyMemory<byte> Value { get; set; }

        public ImmutableArray<SessionIdentifier> ReadLocks
        {
            get => _readLocks.IsDefaultOrEmpty ? ImmutableArray<SessionIdentifier>.Empty : _readLocks;
            set => _readLocks = value;
        }

        public SessionIdentifier? WriteLock { get; set; }

        public int StorageVersion { get; set; }

        public bool IsMarkedAsDeleted { get; set; }
    }
}
