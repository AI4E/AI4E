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
using System.Threading.Tasks;
using AI4E.Coordination.Mocks;
using AI4E.Coordination.Session;
using AI4E.Storage.InMemory;
using AI4E.Utils.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Coordination.Storage
{
    [TestClass]
    public class CoordinationStorageTests
    {
        private static readonly CoordinationSession _session1 = new CoordinationSession(new byte[] { 1, 2, 3 }.AsSpan(), new byte[] { 2, 3, 4 }.AsSpan());
        private static readonly CoordinationSession _session2 = new CoordinationSession(new byte[] { 1, 2, 3 }.AsSpan(), new byte[] { 1, 2, 3, 4, 5, 6, 7 }.AsSpan());
        private static readonly CoordinationSession _session3 = new CoordinationSession(new byte[] { 8, 9 }.AsSpan(), new byte[] { 2, 3, 4 }.AsSpan());

        [TestMethod]
        public async Task GetEntryAsyncFromEmptyStorageTest()
        {
            var database = new InMemoryDatabase();
            var storage = new CoordinationStorage(database);
            var queryResult = await storage.GetEntryAsync("/x/y/", cancellation: default);

            Assert.IsNull(queryResult);
        }

        [TestMethod]
        public async Task EntryRoundtripTest()
        {
            var database = new InMemoryDatabase();
            var storage = new CoordinationStorage(database);
            var entry = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 1,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3
            };

            var updateResult = await storage.UpdateEntryAsync(entry, null, cancellation: default);
            var queryResult = await storage.GetEntryAsync("/x/y/", cancellation: default);

            Assert.IsNull(updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task AddConcurrencyFailureTest()
        {
            var database = new InMemoryDatabase();
            var storage = new CoordinationStorage(database);
            var entry = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 1,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3
            };

            await storage.UpdateEntryAsync(entry, null, cancellation: default);

            var updateResult = await storage.UpdateEntryAsync(entry, null, cancellation: default);
            var queryResult = await storage.GetEntryAsync("/x/y/", cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task RemoveTest()
        {
            var database = new InMemoryDatabase();
            var storage = new CoordinationStorage(database);
            var entry = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 1,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3
            };

            await storage.UpdateEntryAsync(entry, null, cancellation: default);
            var updateResult = await storage.UpdateEntryAsync(null, entry, cancellation: default);
            var queryResult = await storage.GetEntryAsync("/x/y/", cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNull(queryResult);
        }

        [TestMethod]
        public async Task RemoveConcurrencyFailureTest()
        {
            var database = new InMemoryDatabase();
            var storage = new CoordinationStorage(database);
            var entry = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 2,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3
            };

            var comparand = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 1,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3
            };

            await storage.UpdateEntryAsync(entry, null, cancellation: default);
            var updateResult = await storage.UpdateEntryAsync(null, comparand, cancellation: default);
            var queryResult = await storage.GetEntryAsync("/x/y/", cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task UpdateTest()
        {
            var database = new InMemoryDatabase();
            var storage = new CoordinationStorage(database);
            var entry = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 1,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3
            };
            var updated = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 2,
                Value = new byte[] { 2, 3, 4 },
                ReadLocks = new[] { _session3 }.ToImmutableArray(),
                WriteLock = _session1
            };

            await storage.UpdateEntryAsync(entry, null, cancellation: default);
            var updateResult = await storage.UpdateEntryAsync(updated, entry, cancellation: default);
            var queryResult = await storage.GetEntryAsync("/x/y/", cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(updated, queryResult);
        }

        [TestMethod]
        public async Task UpdateConcurrencyFailureTest()
        {
            var database = new InMemoryDatabase();
            var storage = new CoordinationStorage(database);
            var entry = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 2,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3
            };

            var updated = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 3,
                Value = new byte[] { 2, 3, 4 },
                ReadLocks = new[] { _session3 }.ToImmutableArray(),
                WriteLock = _session1
            };

            var comparand = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 1,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3
            };

            await storage.UpdateEntryAsync(entry, null, cancellation: default);
            var updateResult = await storage.UpdateEntryAsync(updated, comparand, cancellation: default);
            var queryResult = await storage.GetEntryAsync("/x/y/", cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task CanHandleDefaultImmutableArrayForReadLocksTest()
        {
            var database = new InMemoryDatabase();
            var storage = new CoordinationStorage(database);
            var entry = new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 1,
                Value = new byte[] { 1, 2, 3 },
                WriteLock = _session3
            };

            var updateResult = await storage.UpdateEntryAsync(entry, null, cancellation: default);
            var queryResult = await storage.GetEntryAsync("/x/y/", cancellation: default);

            Assert.IsNull(updateResult);
            Assert.IsNotNull(queryResult);
            Assert.AreEqual(entry.Key, queryResult.Key);
            Assert.AreEqual(entry.StorageVersion, queryResult.StorageVersion);
            Assert.IsTrue(entry.Value.Span.SequenceEqual(queryResult.Value.Span));
            Assert.IsTrue(entry.ReadLocks.IsDefaultOrEmpty);
            Assert.AreEqual(entry.WriteLock, queryResult.WriteLock);
            Assert.IsFalse(queryResult.IsMarkedAsDeleted);
        }

        private void AssertEquality(IStoredEntry desired, IStoredEntry value)
        {
            Assert.AreEqual(desired.Key, value.Key);
            Assert.AreEqual(desired.StorageVersion, value.StorageVersion);
            Assert.IsTrue(desired.Value.Span.SequenceEqual(value.Value.Span));
            Assert.IsTrue(desired.ReadLocks.SequenceEqual(value.ReadLocks));
            Assert.AreEqual(desired.WriteLock, value.WriteLock);
            Assert.AreEqual(desired.IsMarkedAsDeleted, value.IsMarkedAsDeleted);
        }
    }
}
