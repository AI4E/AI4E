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
using AI4E.Storage.Coordination.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Coordination.Session
{
    [TestClass]
    public class SessionManagerTests
    {
        private static readonly SessionIdentifier _session1 = new SessionIdentifier(new byte[] { 1, 2, 3 }.AsSpan(), new byte[] { 2, 3, 4 }.AsSpan());
        private static readonly SessionIdentifier _session2 = new SessionIdentifier(new byte[] { 1, 2, 3 }.AsSpan(), new byte[] { 1, 2, 3, 4, 5, 6, 7 }.AsSpan());
        private static readonly SessionIdentifier _session3 = new SessionIdentifier(new byte[] { 8, 9 }.AsSpan(), new byte[] { 2, 3, 4 }.AsSpan());

        private static readonly CoordinationEntryPath _entryPath1 = new CoordinationEntryPath("/x/y/z/");
        private static readonly CoordinationEntryPath _entryPath2 = new CoordinationEntryPath("/x/y/");

        public SessionStorageMock SessionStorage { get; set; }
        public DateTimeProviderMock DateTimeProvider { get; set; }
        public IStoredSessionManager StoredSessionManager { get; set; }
        public SessionManager SessionManager { get; set; }

        [TestInitialize]
        public void Setup()
        {
            SessionStorage = new SessionStorageMock();
            DateTimeProvider = new DateTimeProviderMock();
            StoredSessionManager = new StoredSessionManager(DateTimeProvider);
            SessionManager = new SessionManager(SessionStorage, StoredSessionManager, DateTimeProvider);
        }

        [TestMethod]
        public async Task TryBeginSessionTest()
        {
            var result = await SessionManager.TryBeginSessionAsync(_session1, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.IsTrue(result);
            Assert.AreEqual(_session1, entries.Single().Session);
            Assert.AreEqual(DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), entries.Single().LeaseEnd);
            Assert.IsFalse(entries.Single().IsEnded);
            Assert.AreEqual(1, entries.Single().StorageVersion);
            Assert.AreEqual(0, entries.Single().EntryPaths.Count());
        }

        [TestMethod]
        public async Task TryBeginExistingSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var result = await SessionManager.TryBeginSessionAsync(_session1, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.IsFalse(result);
            AssertEquality(existingEntry, entries.Single());
        }

        [TestMethod]
        public async Task TryBeginExistingEndedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var result = await SessionManager.TryBeginSessionAsync(_session1, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.IsFalse(result);
            AssertEquality(existingEntry, entries.Single());
        }

        [TestMethod]
        public async Task UpdateSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await SessionManager.UpdateSessionAsync(_session1, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(_session1, entries.Single().Session);
            Assert.AreEqual(DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), entries.Single().LeaseEnd);
            Assert.IsFalse(entries.Single().IsEnded);
            Assert.AreEqual(11, entries.Single().StorageVersion);
            Assert.IsTrue(entries.Single().EntryPaths.IsDefaultOrEmpty);
        }

        [TestMethod]
        public async Task UpdateNonExistingSessionTest()
        {
            await Assert.ThrowsExceptionAsync<SessionTerminatedException>(async () =>
            {
                await SessionManager.UpdateSessionAsync(_session1, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), cancellation: default);
            });

            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();
            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task UpdateTerminatedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await Assert.ThrowsExceptionAsync<SessionTerminatedException>(async () =>
            {
                await SessionManager.UpdateSessionAsync(_session1, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), cancellation: default);
            });

            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();
            AssertEquality(existingEntry, entries.Single());
        }

        [TestMethod]
        public async Task UpdateEndedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await Assert.ThrowsExceptionAsync<SessionTerminatedException>(async () =>
            {
                await SessionManager.UpdateSessionAsync(_session1, DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(30), cancellation: default);
            });

            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();
            AssertEquality(existingEntry, entries.Single());
        }

        [TestMethod]
        public async Task EndSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await SessionManager.EndSessionAsync(_session1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task EndSessionWithEntriesTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false,
                EntryPaths = new[] { _entryPath1 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await SessionManager.EndSessionAsync(_session1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(_session1, entries.Single().Session);
            Assert.AreEqual(DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20), entries.Single().LeaseEnd);
            Assert.IsTrue(entries.Single().IsEnded);
            Assert.AreEqual(11, entries.Single().StorageVersion);
            Assert.AreEqual(_entryPath1, entries.Single().EntryPaths.Single());
        }

        [TestMethod]
        public async Task EndEndedSessionWithEntriesTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false,
                EntryPaths = new[] { _entryPath1 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await SessionManager.EndSessionAsync(_session1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            AssertEquality(existingEntry, entries.Single());
        }

        [TestMethod]
        public async Task EndEndedSessionWithoutEntriesTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false,
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await SessionManager.EndSessionAsync(_session1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task EndTerminatedSessionWithEntriesTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true,
                EntryPaths = new[] { _entryPath1 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await SessionManager.EndSessionAsync(_session1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            AssertEquality(existingEntry, entries.Single());
        }

        [TestMethod]
        public async Task EndTerminatedSessionWithoutEntriesTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true,
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await SessionManager.EndSessionAsync(_session1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task EndNonExistingSessionTest()
        {
            await SessionManager.EndSessionAsync(_session1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task IsAliveTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var result = await SessionManager.IsAliveAsync(_session1, cancellation: default);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task NonExistingSessionIsAliveTest()
        {
            var result = await SessionManager.IsAliveAsync(_session1, cancellation: default);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task EndedSessionIsAliveTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var result = await SessionManager.IsAliveAsync(_session1, cancellation: default);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task TerminatedSessionIsAliveTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var result = await SessionManager.IsAliveAsync(_session1, cancellation: default);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetSessionsTest()
        {
            var entry1 = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            var entry2 = new StoredSessionMock
            {
                Session = _session2,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            var entry3 = new StoredSessionMock
            {
                Session = _session3,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(entry1, null);
            await SessionStorage.UpdateSessionAsync(entry2, null);
            await SessionStorage.UpdateSessionAsync(entry3, null);

            var entries = await SessionManager.GetSessionsAsync(cancellation: default).ToListAsync();

            Assert.AreEqual(3, entries.Count());
            Assert.IsTrue(entries.ToHashSet().SetEquals(new[] { _session1, _session2, _session3 }));
        }

        [TestMethod]
        public async Task GetSessionsNonAliveTest()
        {
            var entry1 = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            var entry2 = new StoredSessionMock
            {
                Session = _session2,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            var entry3 = new StoredSessionMock
            {
                Session = _session3,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true
            };

            await SessionStorage.UpdateSessionAsync(entry1, null);
            await SessionStorage.UpdateSessionAsync(entry2, null);
            await SessionStorage.UpdateSessionAsync(entry3, null);

            var entries = await SessionManager.GetSessionsAsync(cancellation: default).ToListAsync();

            Assert.AreEqual(1, entries.Count());
            Assert.AreEqual(_session1, entries.Single());
        }

        [TestMethod]
        public async Task AddSessionEntryTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            await SessionManager.AddSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(_entryPath1, entries.Single().EntryPaths.Single());
        }

        [TestMethod]
        public async Task AddExistingSessionEntryTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false,
                EntryPaths = new[] { _entryPath1 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            await SessionManager.AddSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(_entryPath1, entries.Single().EntryPaths.Single());
        }

        [TestMethod]
        public async Task AddSessionEntryToEndedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false,
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await Assert.ThrowsExceptionAsync<SessionTerminatedException>(async () =>
            {
                await SessionManager.AddSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            });

            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.IsTrue(entries.Single().EntryPaths.IsDefaultOrEmpty);
        }

        [TestMethod]
        public async Task AddSessionEntryToTerminatedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true,
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            await Assert.ThrowsExceptionAsync<SessionTerminatedException>(async () =>
            {
                await SessionManager.AddSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            });

            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.IsTrue(entries.Single().EntryPaths.IsDefaultOrEmpty);
        }

        [TestMethod]
        public async Task AddSessionEntryToNonExistingSessionTest()
        {
            await Assert.ThrowsExceptionAsync<SessionTerminatedException>(async () =>
            {
                await SessionManager.AddSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            });

            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task RemoveSessionEntryTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false,
                EntryPaths = new[] { _entryPath1 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            await SessionManager.RemoveSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.IsTrue(entries.Single().EntryPaths.IsDefaultOrEmpty);
        }

        [TestMethod]
        public async Task RemoveNonExistingSessionEntryTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false,

            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            await SessionManager.RemoveSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.IsTrue(entries.Single().EntryPaths.IsDefaultOrEmpty);
        }

        [TestMethod]
        public async Task RemoveSessionEntryFromEndedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false,
                EntryPaths = new[] { _entryPath1, _entryPath2 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            await SessionManager.RemoveSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(_entryPath2, entries.Single().EntryPaths.Single());
        }

        [TestMethod]
        public async Task RemoveSessionEntryFromTerminatedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true,
                EntryPaths = new[] { _entryPath1, _entryPath2 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            await SessionManager.RemoveSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(_entryPath2, entries.Single().EntryPaths.Single());
        }

        [TestMethod]
        public async Task RemoveSessionEntryFromNonExistingSessionTest()
        {
            await SessionManager.RemoveSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task RemoveLastSessionEntryFromEndedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false,
                EntryPaths = new[] { _entryPath1 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            await SessionManager.RemoveSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task RemoveLastSessionEntryFromTerminatedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true,
                EntryPaths = new[] { _entryPath1 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            await SessionManager.RemoveSessionEntryAsync(_session1, _entryPath1, cancellation: default);
            var entries = await SessionStorage.GetSessionsAsync().ToListAsync();

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task GetSessionEntriesTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false,
                EntryPaths = new[] { _entryPath1, _entryPath2 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            var entries = await SessionManager.GetEntriesAsync(_session1, cancellation: default);

            Assert.AreEqual(2, entries.Count());
            Assert.IsTrue(entries.ToHashSet().SetEquals(new[] { _entryPath1, _entryPath2 }));
        }

        [TestMethod]
        public async Task GetSessionEntriesFromEndedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false,
                EntryPaths = new[] { _entryPath1, _entryPath2 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            var entries = await SessionManager.GetEntriesAsync(_session1, cancellation: default);

            Assert.AreEqual(2, entries.Count());
            Assert.IsTrue(entries.ToHashSet().SetEquals(new[] { _entryPath1, _entryPath2 }));
        }

        [TestMethod]
        public async Task GetSessionEntriesFromTerminatedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true,
                EntryPaths = new[] { _entryPath1, _entryPath2 }.ToImmutableArray()
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);
            var entries = await SessionManager.GetEntriesAsync(_session1, cancellation: default);

            Assert.AreEqual(2, entries.Count());
            Assert.IsTrue(entries.ToHashSet().SetEquals(new[] { _entryPath1, _entryPath2 }));
        }

        [TestMethod]
        public async Task GetSessionEntriesFromNonExistingSessionTest()
        {
            var entries = await SessionManager.GetEntriesAsync(_session1, cancellation: default);

            Assert.AreEqual(0, entries.Count());
        }

        [TestMethod]
        public async Task WaitForTerminationTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var task = SessionManager.WaitForTerminationAsync(_session1, cancellation: default);

            Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status);
        }

        [TestMethod]
        public void WaitForTerminationNonExitingSessionTest()
        {
            var task = SessionManager.WaitForTerminationAsync(_session1, cancellation: default);

            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminationTerminatedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = true
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var task = SessionManager.WaitForTerminationAsync(_session1, cancellation: default);

            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminationEndedSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime - TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var task = SessionManager.WaitForTerminationAsync(_session1, cancellation: default);

            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminationEndingSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var task = SessionManager.WaitForTerminationAsync(_session1, cancellation: default);

            DateTimeProvider.CurrentTime += TimeSpan.FromMilliseconds(20);
            await Task.Delay(20);

            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminationTerminatingSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            var updatedEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 11,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(35),
                IsEnded = true
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var task = SessionManager.WaitForTerminationAsync(_session1, cancellation: default);

            await SessionStorage.UpdateSessionAsync(updatedEntry, existingEntry);

            DateTimeProvider.CurrentTime += TimeSpan.FromMilliseconds(20);
            await Task.Delay(20);

            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminationUpdatingSessionTest()
        {
            var existingEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            var updatedEntry = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 11,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(35),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(existingEntry, null);

            var task = SessionManager.WaitForTerminationAsync(_session1, cancellation: default);

            await SessionStorage.UpdateSessionAsync(updatedEntry, existingEntry);

            DateTimeProvider.CurrentTime += TimeSpan.FromMilliseconds(20);
            await Task.Delay(20);

            Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminatedSessionsTest()
        {
            var entry1 = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            var entry2 = new StoredSessionMock
            {
                Session = _session2,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromSeconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(entry1, null);
            await SessionStorage.UpdateSessionAsync(entry2, null);

            var task = SessionManager.WaitForTerminationAsync(cancellation: default);

            Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminatedSessionsUpdatedTest()
        {
            var entry1 = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            var entry2 = new StoredSessionMock
            {
                Session = _session2,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            var entry1Updated = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 11,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(20),
                IsEnded = false
            };

            var entry2Updated = new StoredSessionMock
            {
                Session = _session2,
                StorageVersion = 11,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(entry1, null);
            await SessionStorage.UpdateSessionAsync(entry2, null);

            var task = SessionManager.WaitForTerminationAsync(cancellation: default);

            await SessionStorage.UpdateSessionAsync(entry1Updated, entry1);
            await SessionStorage.UpdateSessionAsync(entry2Updated, entry2);

            DateTimeProvider.CurrentTime += TimeSpan.FromMilliseconds(15);
            await Task.Delay(20);

            Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminatedSessionsTerminatedTest()
        {
            var entry1 = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            var entry2 = new StoredSessionMock
            {
                Session = _session2,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            var entry1Updated = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 11,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(20),
                IsEnded = false
            };

            var entry2Updated = new StoredSessionMock
            {
                Session = _session2,
                StorageVersion = 11,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(20),
                IsEnded = true
            };

            await SessionStorage.UpdateSessionAsync(entry1, null);
            await SessionStorage.UpdateSessionAsync(entry2, null);

            var task = SessionManager.WaitForTerminationAsync(cancellation: default);

            await SessionStorage.UpdateSessionAsync(entry1Updated, entry1);
            await SessionStorage.UpdateSessionAsync(entry2Updated, entry2);

            DateTimeProvider.CurrentTime += TimeSpan.FromMilliseconds(15);
            await Task.Delay(20);

            var terminated = await task;

            Assert.AreEqual(_session2, terminated);
        }

        [TestMethod]
        public async Task WaitForTerminatedSessionsEndedTest()
        {
            var entry1 = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            var entry2 = new StoredSessionMock
            {
                Session = _session2,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            var entry1Updated = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 11,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(20),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(entry1, null);
            await SessionStorage.UpdateSessionAsync(entry2, null);

            var task = SessionManager.WaitForTerminationAsync(cancellation: default);

            await SessionStorage.UpdateSessionAsync(entry1Updated, entry1);

            DateTimeProvider.CurrentTime += TimeSpan.FromMilliseconds(15);
            await Task.Delay(20);

            var terminated = await task;

            Assert.AreEqual(_session2, terminated);
        }

        [TestMethod]
        public void WaitForTerminatedSessionsNoSessionsTest()
        {
            var task = SessionManager.WaitForTerminationAsync(cancellation: default);
            Assert.AreEqual(TaskStatus.WaitingForActivation, task.Status);
        }

        [TestMethod]
        public async Task WaitForTerminatedSessionsNoSessions2Test()
        {
            var task = SessionManager.WaitForTerminationAsync(cancellation: default);

            var entry1 = new StoredSessionMock
            {
                Session = _session1,
                StorageVersion = 10,
                LeaseEnd = DateTimeProvider.CurrentTime + TimeSpan.FromMilliseconds(5),
                IsEnded = false
            };

            await SessionStorage.UpdateSessionAsync(entry1, null);

            DateTimeProvider.CurrentTime += TimeSpan.FromMilliseconds(15);
            await Task.Delay(20);

            var terminated = await task;

            Assert.AreEqual(_session1, terminated);
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
