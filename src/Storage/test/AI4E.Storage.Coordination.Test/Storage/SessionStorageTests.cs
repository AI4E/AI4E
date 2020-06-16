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
using AI4E.Storage.Coordination.Mocks;
using AI4E.Storage.Coordination.Session;
using AI4E.Storage.InMemory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Coordination.Storage
{
    [TestClass]
    public class SessionStorageTests
    {
        private static readonly SessionIdentifier _session1 = new SessionIdentifier(new byte[] { 1, 2, 3 }.AsSpan(), new byte[] { 2, 3, 4 }.AsSpan());
        private static readonly SessionIdentifier _session2 = new SessionIdentifier(new byte[] { 1, 2, 3 }.AsSpan(), new byte[] { 1, 2, 3, 4, 5, 6, 7 }.AsSpan());

        private static readonly CoordinationEntryPath _entryPath1 = new CoordinationEntryPath("/x/y/z/");
        private static readonly CoordinationEntryPath _entryPath2 = new CoordinationEntryPath("/x/y/");

        [TestMethod]
        public async Task GetSessionAsyncFromEmptyStorageTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var queryResult = await storage.GetSessionAsync(_session1, cancellation: default);

            Assert.IsNull(queryResult);
        }

        [TestMethod]
        public async Task EntryRoundtripTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry = BuildDummyEntry();
            var updateResult = await storage.UpdateSessionAsync(entry, null, cancellation: default);
            var queryResult = await storage.GetSessionAsync(_session1, cancellation: default);

            Assert.IsNull(updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task AddConcurrencyFailureTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry = BuildDummyEntry();

            await storage.UpdateSessionAsync(entry, null, cancellation: default);

            var updateResult = await storage.UpdateSessionAsync(entry, null, cancellation: default);
            var queryResult = await storage.GetSessionAsync(_session1, cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task RemoveTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry = BuildDummyEntry();

            await storage.UpdateSessionAsync(entry, null, cancellation: default);
            var updateResult = await storage.UpdateSessionAsync(null, entry, cancellation: default);
            var queryResult = await storage.GetSessionAsync(_session1, cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNull(queryResult);
        }

        [TestMethod]
        public async Task RemoveConcurrencyFailureTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry = BuildDummyEntry();

            var comparand = BuildDummyEntry();
            comparand.StorageVersion = 1;

            await storage.UpdateSessionAsync(entry, null, cancellation: default);
            var updateResult = await storage.UpdateSessionAsync(null, comparand, cancellation: default);
            var queryResult = await storage.GetSessionAsync(_session1, cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task UpdateTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry = BuildDummyEntry();
            var updated = BuildDummyEntry();
            updated.LeaseEnd = DateTime.MinValue;
            updated.EntryPaths = new[] { _entryPath1 }.ToImmutableArray();
            updated.StorageVersion = 3;

            await storage.UpdateSessionAsync(entry, null, cancellation: default);
            var updateResult = await storage.UpdateSessionAsync(updated, entry, cancellation: default);
            var queryResult = await storage.GetSessionAsync(_session1, cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(updated, queryResult);
        }

        [TestMethod]
        public async Task UpdateConcurrencyFailureTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry = BuildDummyEntry();
            var updated = BuildDummyEntry();
            updated.LeaseEnd = DateTime.MinValue;
            updated.EntryPaths = new[] { _entryPath1 }.ToImmutableArray();
            updated.StorageVersion = 3;

            var comparand = BuildDummyEntry();
            comparand.StorageVersion = 1;

            await storage.UpdateSessionAsync(entry, null, cancellation: default);
            var updateResult = await storage.UpdateSessionAsync(updated, comparand, cancellation: default);
            var queryResult = await storage.GetSessionAsync(_session1, cancellation: default);

            Assert.IsNotNull(updateResult);
            AssertEquality(entry, updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task GetSessionsAsyncTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry1 = BuildDummyEntry();
            var entry2 = BuildDummyEntry();
            entry2.Session = _session2;

            await storage.UpdateSessionAsync(entry1, null, cancellation: default);
            await storage.UpdateSessionAsync(entry2, null, cancellation: default);
            var queryResults = await storage.GetSessionsAsync(cancellation: default).ToListAsync();

            Assert.AreEqual(2, queryResults.Count);
            AssertEquality(entry1, queryResults.Single(p => p.Session == _session1));
            AssertEquality(entry2, queryResults.Single(p => p.Session == _session2));
        }

        [TestMethod]
        public async Task UpdateWithDefaultEntryPathsImmutableArrayTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry = BuildDummyEntry();
            entry.EntryPaths = default;
            var updateResult = await storage.UpdateSessionAsync(entry, null, cancellation: default);
            var queryResult = await storage.GetSessionAsync(_session1, cancellation: default);

            Assert.IsNull(updateResult);
            Assert.IsNotNull(queryResult);
            AssertEquality(entry, queryResult);
        }

        [TestMethod]
        public async Task UpdateDifferentKeysTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);
            var entry = new StoredSessionMock { Session = _session1 };
            var comparand = new StoredSessionMock { Session = _session2 };

            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await storage.UpdateSessionAsync(entry, comparand, cancellation: default);
            });
        }

        [TestMethod]
        public async Task UpdateBothNullTest()
        {
            var database = new InMemoryDatabase();
            var storage = new SessionStorage(database);

            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await storage.UpdateSessionAsync(null, null, cancellation: default);
            });
        }

        private StoredSessionMock BuildDummyEntry()
        {
            var entry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 2,
                LeaseEnd = DateTime.Now,
                EntryPaths = new[] { _entryPath1, _entryPath2 }.ToImmutableArray()
            };

            return entry;
        }

        private void AssertEquality(IStoredSession desired, IStoredSession value)
        {
            Assert.AreEqual(desired.Session, value.Session);
            Assert.AreEqual(desired.StorageVersion, value.StorageVersion);
            Assert.IsTrue(desired.EntryPaths.IsDefaultOrEmpty && value.EntryPaths.IsDefaultOrEmpty ||
                       !desired.EntryPaths.IsDefaultOrEmpty && !value.EntryPaths.IsDefaultOrEmpty && desired.EntryPaths.SequenceEqual(value.EntryPaths));
            Assert.AreEqual(desired.LeaseEnd, value.LeaseEnd);
            Assert.AreEqual(desired.IsEnded, value.IsEnded);
        }
    }
}
