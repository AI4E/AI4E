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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.InMemory
{
    [TestClass]
    public class InMemoryDatabaseTests
    {
        [TestMethod]
        public void CreateScopeThrowsTest()
        {
            var database = new InMemoryDatabase();

            Assert.ThrowsException<NotSupportedException>(() =>
            {
                ((IDatabase)database).CreateScope();
            });
        }

        [TestMethod]
        public void SupportsScopesIsFalseTest()
        {
            var database = new InMemoryDatabase();

            Assert.IsFalse(((IDatabase)database).SupportsScopes);
        }

        [TestMethod]
        public async Task GetAsyncEmptyDatabaseTest()
        {
            var database = new InMemoryDatabase();
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.AreEqual(0, queryResults.Count);
        }

        [TestMethod]
        public async Task GetOneAsyncEmptyDatabaseTest()
        {
            var database = new InMemoryDatabase();
            var queryResult = await database.GetOneAsync<TestEntry>(p => true, cancellation: default);

            Assert.IsNull(queryResult);
        }

        [TestMethod]
        public async Task SingleEntryRountripTest()
        {
            var database = new InMemoryDatabase();
            var entry = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var addSuccess = await database.AddAsync(entry, cancellation: default);
            var queryResult = await database.GetOneAsync<TestEntry>(p => true, cancellation: default);

            Assert.IsTrue(addSuccess);
            Assert.IsNotNull(queryResult);
            Assert.AreEqual(entry.Id, queryResult.Id);
            Assert.AreEqual(entry.String, queryResult.String);
            Assert.AreEqual(entry.Int, queryResult.Int);
        }

        [TestMethod]
        public async Task MultipleEntriesRountripTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "myString", Int = 1234 };
            var addSuccess1 = await database.AddAsync(entry1, cancellation: default);
            var addSuccess2 = await database.AddAsync(entry2, cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsTrue(addSuccess1);
            Assert.IsTrue(addSuccess2);
            Assert.AreEqual(2, queryResults.Count);
            Assert.AreEqual(entry1.String, queryResults[0].String);
            Assert.AreEqual(entry1.Int, queryResults[0].Int);
            Assert.AreEqual(entry2.String, queryResults[1].String);
            Assert.AreEqual(entry2.Int, queryResults[1].Int);
            Assert.IsTrue(queryResults.Select(p => p.Id).OrderBy(p => p).SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public async Task AddExistingIdTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 1, String = "xxx", Int = 345 };
            var addSuccess1 = await database.AddAsync(entry1, cancellation: default);
            var addSuccess2 = await database.AddAsync(entry2, cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsTrue(addSuccess1);
            Assert.IsFalse(addSuccess2);
            Assert.AreEqual(1, queryResults.Count);
            Assert.AreEqual(entry1.String, queryResults[0].String);
            Assert.AreEqual(entry1.Int, queryResults[0].Int);
        }

        [TestMethod]
        public async Task RemoveUnconstraintTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "xxx", Int = 345 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            var removeSuccess = await database.RemoveAsync(new TestEntry { Id = 2 }, _ => true, cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsTrue(removeSuccess);
            Assert.AreEqual(1, queryResults.Count);
            Assert.AreEqual(entry1.String, queryResults[0].String);
            Assert.AreEqual(entry1.Int, queryResults[0].Int);
        }

        [TestMethod]
        public async Task RemoveMatchedConstraintTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "xxx", Int = 345 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            var removeSuccess = await database.RemoveAsync(new TestEntry { Id = 2 }, e => e.String == "xxx", cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsTrue(removeSuccess);
            Assert.AreEqual(1, queryResults.Count);
            Assert.AreEqual(entry1.String, queryResults[0].String);
            Assert.AreEqual(entry1.Int, queryResults[0].Int);
        }

        [TestMethod]
        public async Task RemoveUnmatchedConstraintTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "xxx", Int = 345 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            var removeSuccess = await database.RemoveAsync(new TestEntry { Id = 2 }, e => e.String == "yyy", cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsFalse(removeSuccess);
            Assert.AreEqual(2, queryResults.Count);
            Assert.AreEqual(entry1.String, queryResults[0].String);
            Assert.AreEqual(entry1.Int, queryResults[0].Int);
            Assert.AreEqual(entry2.String, queryResults[1].String);
            Assert.AreEqual(entry2.Int, queryResults[1].Int);
            Assert.IsTrue(queryResults.Select(p => p.Id).OrderBy(p => p).SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public async Task RemoveNonExistingTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "myString", Int = 1234 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            var removeSuccess = await database.RemoveAsync(new TestEntry { Id = 3 }, _ => true, cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsFalse(removeSuccess);
            Assert.AreEqual(2, queryResults.Count);
            Assert.AreEqual(entry1.String, queryResults[0].String);
            Assert.AreEqual(entry1.Int, queryResults[0].Int);
            Assert.AreEqual(entry2.String, queryResults[1].String);
            Assert.AreEqual(entry2.Int, queryResults[1].Int);
            Assert.IsTrue(queryResults.Select(p => p.Id).OrderBy(p => p).SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public async Task ClearTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "myString", Int = 1234 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            await database.Clear<TestEntry>(cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.AreEqual(0, queryResults.Count);
        }

        [TestMethod]
        public async Task AddAfterClearTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "myString", Int = 1234 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            await database.Clear<TestEntry>(cancellation: default);
            var addSuccess = await database.AddAsync(entry1, cancellation: default);
            var queryResult = await database.GetOneAsync<TestEntry>(p => true, cancellation: default);

            Assert.IsTrue(addSuccess);
            Assert.IsNotNull(queryResult);
            Assert.AreEqual(entry1.Id, queryResult.Id);
            Assert.AreEqual(entry1.String, queryResult.String);
            Assert.AreEqual(entry1.Int, queryResult.Int);
        }

        [TestMethod]
        public async Task UpdateUnconstraintTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "myString", Int = 1234 };
            var updatedEntry = new TestEntry { Id = 2, String = "xxx", Int = 5 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            var updateSuccess = await database.UpdateAsync(updatedEntry, _ => true, cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsTrue(updateSuccess);
            Assert.AreEqual(2, queryResults.Count);

            Assert.AreEqual(entry1.String, queryResults.Single(p => p.Id == 1).String);
            Assert.AreEqual(entry1.Int, queryResults.Single(p => p.Id == 1).Int);
            Assert.AreEqual(updatedEntry.String, queryResults.Single(p => p.Id == 2).String);
            Assert.AreEqual(updatedEntry.Int, queryResults.Single(p => p.Id == 2).Int);
            Assert.IsTrue(queryResults.Select(p => p.Id).OrderBy(p => p).SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public async Task UpdateMatchedConstraintTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "myString", Int = 1234 };
            var updatedEntry = new TestEntry { Id = 2, String = "xxx", Int = 5 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            var updateSuccess = await database.UpdateAsync(updatedEntry, e => e.String == "myString", cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsTrue(updateSuccess);
            Assert.AreEqual(2, queryResults.Count);

            Assert.AreEqual(entry1.String, queryResults.Single(p => p.Id == 1).String);
            Assert.AreEqual(entry1.Int, queryResults.Single(p => p.Id == 1).Int);
            Assert.AreEqual(updatedEntry.String, queryResults.Single(p => p.Id == 2).String);
            Assert.AreEqual(updatedEntry.Int, queryResults.Single(p => p.Id == 2).Int);
            Assert.IsTrue(queryResults.Select(p => p.Id).OrderBy(p => p).SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public async Task UpdateUnmatchedConstraintTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "myString", Int = 1234 };
            var updatedEntry = new TestEntry { Id = 2, String = "xxx", Int = 5 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            var updateSuccess = await database.UpdateAsync(updatedEntry, e => e.String == "yyy", cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsFalse(updateSuccess);
            Assert.AreEqual(2, queryResults.Count);

            Assert.AreEqual(entry1.String, queryResults.Single(p => p.Id == 1).String);
            Assert.AreEqual(entry1.Int, queryResults.Single(p => p.Id == 1).Int);
            Assert.AreEqual(entry2.String, queryResults.Single(p => p.Id == 2).String);
            Assert.AreEqual(entry2.Int, queryResults.Single(p => p.Id == 2).Int);
            Assert.IsTrue(queryResults.Select(p => p.Id).OrderBy(p => p).SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public async Task UpdateNonExistingTest()
        {
            var database = new InMemoryDatabase();
            var entry1 = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var entry2 = new TestEntry { Id = 2, String = "myString", Int = 1234 };
            await database.AddAsync(entry1, cancellation: default);
            await database.AddAsync(entry2, cancellation: default);
            var updateSuccess = await database.UpdateAsync(new TestEntry { Id = 3 }, _ => true, cancellation: default);
            var queryResults = await database.GetAsync<TestEntry>(p => true, cancellation: default).ToList();

            Assert.IsFalse(updateSuccess);
            Assert.AreEqual(2, queryResults.Count);
            Assert.AreEqual(entry1.String, queryResults[0].String);
            Assert.AreEqual(entry1.Int, queryResults[0].Int);
            Assert.AreEqual(entry2.String, queryResults[1].String);
            Assert.AreEqual(entry2.Int, queryResults[1].Int);
            Assert.IsTrue(queryResults.Select(p => p.Id).OrderBy(p => p).SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public async Task EntriesAreCopiedTest()
        {
            var database = new InMemoryDatabase();
            var entry = new TestEntry { Id = 1, String = "myString", Int = 1234 };
            var addSuccess = await database.AddAsync(entry, cancellation: default);
            var queryResult = await database.GetOneAsync<TestEntry>(p => true, cancellation: default);

            Assert.AreNotSame(entry, queryResult);
        }

        [TestMethod]
        public async Task AddEntryWithoutIdThrowsTest()
        {
            var database = new InMemoryDatabase();
            var entry = new TestEntryWithoutId { String = "myString", Int = 1234 };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await database.AddAsync(entry, cancellation: default);
            });
        }

        [TestMethod]
        public async Task UpdateEntryWithoutIdThrowsTest()
        {
            var database = new InMemoryDatabase();
            var entry = new TestEntryWithoutId { String = "myString", Int = 1234 };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await database.UpdateAsync(entry, cancellation: default);
            });
        }

        [TestMethod]
        public async Task RemoveAsyncEntryWithoutIdThrowsTest()
        {
            var database = new InMemoryDatabase();
            var entry = new TestEntryWithoutId { String = "myString", Int = 1234 };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await database.RemoveAsync(entry, _ => true, cancellation: default);
            });
        }

        [TestMethod]
        public void GetAsyncEntryWithoutIdThrowsTest()
        {
            var database = new InMemoryDatabase();
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                database.GetAsync<TestEntryWithoutId>(_ => true, cancellation: default);
            });
        }

        [TestMethod]
        public async Task GetOneAsyncEntryWithoutIdThrowsTest()
        {
            var database = new InMemoryDatabase();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await database.GetOneAsync<TestEntryWithoutId>(_ => true, cancellation: default);
            });
        }
    }

    public class TestEntry
    {
        public int Id { get; set; }
        public string String { get; set; }
        public int Int { get; set; }
    }

    public class TestEntryWithoutId
    {
        public string String { get; set; }
        public int Int { get; set; }
    }
}
