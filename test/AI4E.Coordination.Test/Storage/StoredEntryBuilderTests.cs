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
using AI4E.Coordination.Mocks;
using AI4E.Coordination.Session;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Coordination.Storage
{
    [TestClass]
    public class StoredEntryBuilderTests
    {
        private static readonly CoordinationSession _session1 = new CoordinationSession(new byte[] { 1, 2, 3 }.AsSpan(), new byte[] { 2, 3, 4 }.AsSpan());
        private static readonly CoordinationSession _session2 = new CoordinationSession(new byte[] { 1, 2, 3 }.AsSpan(), new byte[] { 1, 2, 3, 4, 5, 6, 7 }.AsSpan());
        private static readonly CoordinationSession _session3 = new CoordinationSession(new byte[] { 8, 9 }.AsSpan(), new byte[] { 2, 3, 4 }.AsSpan());

        [TestMethod]
        public void InstantiateFromEntryTest()
        {
            var entry = CreateDummyStoredEntry();
            var builder = new StoredEntryBuilder(entry, _session1);

            Assert.AreEqual(entry.Key, builder.Key);
            Assert.IsTrue(entry.ReadLocks.ToHashSet().SetEquals(builder.ReadLocks));
            Assert.AreEqual(entry.WriteLock, builder.WriteLock);
            Assert.AreEqual(entry.StorageVersion, builder.StorageVersion);
            Assert.AreEqual(entry.IsMarkedAsDeleted, builder.IsMarkedAsDeleted);
            Assert.IsFalse(builder.ChangesPending);
        }

        [TestMethod]
        public void InstantiateFromEntry2Test()
        {
            var entry = CreateDummyStoredEntry2();
            var builder = new StoredEntryBuilder(entry, _session1);

            Assert.AreEqual(entry.Key, builder.Key);
            Assert.IsTrue(entry.ReadLocks.ToHashSet().SetEquals(builder.ReadLocks));
            Assert.AreEqual(entry.WriteLock, builder.WriteLock);
            Assert.AreEqual(entry.StorageVersion, builder.StorageVersion);
            Assert.AreEqual(entry.IsMarkedAsDeleted, builder.IsMarkedAsDeleted);
            Assert.IsFalse(builder.ChangesPending);
        }

        [TestMethod]
        public void InstantiateNonExistingTest()
        {
            var key = "/a/b/";
            var builder = new StoredEntryBuilder(key, _session1);

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(null, builder.WriteLock);
            Assert.AreEqual(0, builder.StorageVersion);
            Assert.IsTrue(builder.IsMarkedAsDeleted);
            Assert.IsFalse(builder.ChangesPending);
        }

        [TestMethod]
        public void EntryRoundtripTest()
        {
            var entry = CreateDummyStoredEntry();
            var builder = new StoredEntryBuilder(entry, _session1);
            var created = builder.ToImmutable(reset: false);

            Assert.AreEqual(entry.Key, created.Key);
            Assert.IsTrue(entry.ReadLocks.ToHashSet().SetEquals(created.ReadLocks));
            Assert.AreEqual(entry.WriteLock, created.WriteLock);
            Assert.AreEqual(entry.StorageVersion, created.StorageVersion);
            Assert.AreEqual(entry.IsMarkedAsDeleted, created.IsMarkedAsDeleted);
        }

        [TestMethod]
        public void EntryRoundtrip2Test()
        {
            var entry = CreateDummyStoredEntry2();
            var builder = new StoredEntryBuilder(entry, _session1);
            var created = builder.ToImmutable(reset: false);

            Assert.AreEqual(entry.Key, created.Key);
            Assert.IsTrue(entry.ReadLocks.ToHashSet().SetEquals(created.ReadLocks));
            Assert.AreEqual(entry.WriteLock, created.WriteLock);
            Assert.AreEqual(entry.StorageVersion, created.StorageVersion);
            Assert.AreEqual(entry.IsMarkedAsDeleted, created.IsMarkedAsDeleted);
        }

        [TestMethod]
        public void CreateTest()
        {
            var key = "/a/b/";
            var builder = new StoredEntryBuilder(key, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            builder.Create(value);

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session1, builder.ReadLocks.Single());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void CreateWithOwnLocksTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
                ReadLocks = new[] { _session1 }.ToImmutableArray(),
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            builder.Create(value);

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session1, builder.ReadLocks.Single());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void CreateExistingThrowsTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key
            };

            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.Create(value);
            });
        }

        [TestMethod]
        public void CreateWithReadLocksThrowsTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                ReadLocks = new[] { _session2 }.ToImmutableArray(),
                IsMarkedAsDeleted = true
            };

            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.Create(value);
            });
        }

        [TestMethod]
        public void CreateWithWriteLockThrowsTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                WriteLock = _session2,
                IsMarkedAsDeleted = true
            };

            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.Create(value);
            });
        }

        [TestMethod]
        public void AcquireWriteLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = default
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.AcquireWriteLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireWriteLockWhenOwningReadLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = new[] { _session1 }.ToImmutableArray(),
                WriteLock = default
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.AcquireWriteLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session1, builder.ReadLocks.Single());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireWriteLockWhenOwningWriteLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.AcquireWriteLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(0, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsFalse(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireWriteLockWhenReadLocksOwnedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = new[] { _session2 }.ToImmutableArray(),
                WriteLock = default
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.AcquireWriteLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session2, builder.ReadLocks.Single());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireWriteLockWhenWriteLockOwnedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = _session2
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.AcquireWriteLock();
            });
        }

        [TestMethod]
        public void AcquireWriteLockWhenNonExistentTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
            };
            var builder = new StoredEntryBuilder(entry, _session1);
            builder.AcquireWriteLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsTrue(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireReadLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = default
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.AcquireReadLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session1, builder.ReadLocks.Single());
            Assert.AreEqual(null, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireReadLockWhenOwningReadLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = new[] { _session1 }.ToImmutableArray(),
                WriteLock = default
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.AcquireReadLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session1, builder.ReadLocks.Single());
            Assert.AreEqual(null, builder.WriteLock);
            Assert.AreEqual(0, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsFalse(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireReadLockWhenOwningWriteLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.AcquireReadLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session1, builder.ReadLocks.Single());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireReadLockWhenReadLocksOwnedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = new[] { _session2 }.ToImmutableArray(),
                WriteLock = default
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.AcquireReadLock();

            Assert.AreEqual(key, builder.Key);
            Assert.IsTrue(builder.ReadLocks.ToHashSet().SetEquals(new[] { _session1, _session2 }));
            Assert.AreEqual(null, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void AcquireReadLockWhenWriteLockOwnedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = _session2
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.AcquireReadLock();
            });
        }

        [TestMethod]
        public void AcquireReadLockWhenNonExistentTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.AcquireReadLock();
            });
        }

        [TestMethod]
        public void ReleaseWriteLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.ReleaseWriteLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(null, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void ReleaseWriteLockIfNotOwnedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = null
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.ReleaseWriteLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(null, builder.WriteLock);
            Assert.AreEqual(0, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsFalse(builder.ChangesPending);
        }

        [TestMethod]
        public void ReleaseWriteLockThrowsWhenOwnedByOtherTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = _session2
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.ReleaseWriteLock();
            });
        }

        [TestMethod]
        public void ReleaseReadLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = null
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.ReleaseReadLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session2, builder.ReadLocks.Single());
            Assert.AreEqual(null, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void ReleaseReadLockIfNotOwnedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = new[] { _session2 }.ToImmutableArray(),
                WriteLock = null
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.ReleaseReadLock();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session2, builder.ReadLocks.Single());
            Assert.AreEqual(null, builder.WriteLock);
            Assert.AreEqual(0, builder.StorageVersion);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsFalse(builder.ChangesPending);
        }

        [TestMethod]
        public void MarkAsDeletedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.MarkAsDeleted();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsTrue(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void MarkAsDeletedWhenOwningReadLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = new[] { _session1 }.ToImmutableArray(),
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.MarkAsDeleted();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session1, builder.ReadLocks.Single());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.IsTrue(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void MarkAsDeletedWhenNonExistentTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
                ReadLocks = default,
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            builder.MarkAsDeleted();

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(0, builder.StorageVersion);
            Assert.IsTrue(builder.IsMarkedAsDeleted);
            Assert.IsFalse(builder.ChangesPending);
        }

        [TestMethod]
        public void MarkAsDeletedWhenNotOwningWriteLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
                ReadLocks = default,
                WriteLock = _session2
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.MarkAsDeleted();
            });
        }

        [TestMethod]
        public void MarkAsDeletedWhenReadLocksOwnedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
                ReadLocks = new[] { _session2 }.ToImmutableArray(),
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.MarkAsDeleted();
            });
        }

        [TestMethod]
        public void SetValueTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = default,
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            builder.SetValue(value);

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(0, builder.ReadLocks.Count());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.AreEqual(value, builder.Value);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void SetValueWhenOwningReadLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = false,
                ReadLocks = new[] { _session1 }.ToImmutableArray(),
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            builder.SetValue(value);

            Assert.AreEqual(key, builder.Key);
            Assert.AreEqual(_session1, builder.ReadLocks.Single());
            Assert.AreEqual(_session1, builder.WriteLock);
            Assert.AreEqual(1, builder.StorageVersion);
            Assert.AreEqual(value, builder.Value);
            Assert.IsFalse(builder.IsMarkedAsDeleted);
            Assert.IsTrue(builder.ChangesPending);
        }

        [TestMethod]
        public void SetValueWhenNotOwningWriteLockTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
                ReadLocks = default,
                WriteLock = _session2
            };
            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.SetValue(value);
            });
        }

        [TestMethod]
        public void SetValueWhenReadLocksOwnedTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
                ReadLocks = new[] { _session2 }.ToImmutableArray(),
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.SetValue(value);
            });
        }

        [TestMethod]
        public void SetValueWhenNotExistentTest()
        {
            var key = "/a/b/";
            var entry = new StoredEntryMock
            {
                Key = key,
                IsMarkedAsDeleted = true,
                ReadLocks = default,
                WriteLock = _session1
            };
            var builder = new StoredEntryBuilder(entry, _session1);
            var value = (ReadOnlyMemory<byte>)new byte[] { 1, 2, 3 }.AsMemory();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                builder.SetValue(value);
            });
        }

        private static StoredEntryMock CreateDummyStoredEntry()
        {
            return new StoredEntryMock
            {
                Key = "/x/y/",
                StorageVersion = 1,
                Value = new byte[] { 1, 2, 3 },
                ReadLocks = new[] { _session1, _session2 }.ToImmutableArray(),
                WriteLock = _session3,
                IsMarkedAsDeleted = true
            };
        }

        private static StoredEntryMock CreateDummyStoredEntry2()
        {
            return new StoredEntryMock
            {
                Key = "/",
                StorageVersion = 5,
                Value = ReadOnlyMemory<byte>.Empty,
                ReadLocks = default,
                WriteLock = default
            };
        }
    }
}
