using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Storage.Domain;
using AI4E.Storage.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace AI4E.Storage
{
    [TestClass]
    public class EntityStorageTest
    {
        [TestMethod]
        public async Task TestEntityStorage()
        {
            var services = Setup.BuildDefaultInMemorySetup();

            Guid id;
            string concurrencyToken;

            using (var scope = services.CreateScope())
            using (var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var aggregate = new TestAggregate();
                var success = await storageEngine.TryStoreAsync(typeof(TestAggregate), aggregate, aggregate.Id.ToString());

                Assert.IsTrue(success);
                Assert.AreNotEqual(Guid.Empty, aggregate.ConcurrencyToken);

                id = aggregate.Id;
                concurrencyToken = aggregate.ConcurrencyToken;
            }


            using (var scope = services.CreateScope())
            using (var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var aggregate = await storageEngine.GetByIdAsync(typeof(TestAggregate), id.ToString()) as TestAggregate;
                Assert.IsNotNull(aggregate);
                Assert.AreEqual(id, aggregate.Id);
                Assert.AreEqual(1, aggregate.Revision);
                Assert.AreEqual(concurrencyToken, aggregate.ConcurrencyToken);
                Assert.IsNotNull(aggregate.UncommittedEvents);
                Assert.AreEqual(0, aggregate.UncommittedEvents.Count());

                aggregate.TestOperation("testValue");

                var success = await storageEngine.TryStoreAsync(typeof(TestAggregate), aggregate, aggregate.Id.ToString());

                Assert.AreNotEqual(Guid.Empty, aggregate.ConcurrencyToken);
                Assert.AreNotEqual(concurrencyToken, aggregate.ConcurrencyToken);

                Assert.IsTrue(success);
            }

            using (var scope = services.CreateScope())
            using (var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var aggregate = await storageEngine.GetByIdAsync(typeof(TestAggregate), id.ToString()) as TestAggregate;
                Assert.IsNotNull(aggregate);
                Assert.AreEqual(id, aggregate.Id);
                Assert.AreEqual(2, aggregate.Revision);
                Assert.AreNotEqual(Guid.Empty, aggregate.ConcurrencyToken);
                Assert.IsNotNull(aggregate.UncommittedEvents);
                Assert.AreEqual(0, aggregate.UncommittedEvents.Count());

                aggregate.TestOperation("testValue");

                // We modify the concurrency token in order to simulate a concurrency conflict.
                aggregate.ConcurrencyToken = concurrencyToken;

                var success = await storageEngine.TryStoreAsync(typeof(TestAggregate), aggregate, aggregate.Id.ToString());

                Assert.IsFalse(success);
            }
        }

        [TestMethod]
        public async Task TestEventDispatch()
        {
            var services = Setup.BuildDefaultInMemorySetup();
            var messageDispatcher = services.GetRequiredService<IMessageDispatcher>();
            var messageHandlingSource = new TaskCompletionSource<TestOperationDoneEvent>();

            messageDispatcher.Register<TestOperationDoneEvent>((message, cancellation) =>
            {
                messageHandlingSource.SetResult(message);

                return new ValueTask<IDispatchResult>(Task.FromResult<IDispatchResult>(new SuccessDispatchResult()));
            });

            Guid id;

            using (var scope = services.CreateScope())
            using (var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>())
            {
                var aggregate = new TestAggregate();
                aggregate.TestOperation("testValue");

                var success = await storageEngine.TryStoreAsync(typeof(TestAggregate), aggregate, aggregate.Id.ToString());

                Assert.IsTrue(success);
                Assert.AreNotEqual(Guid.Empty, aggregate.ConcurrencyToken);

                id = aggregate.Id;
            }

            var dispatchedEvent = await messageHandlingSource.Task.WithCancellation(new CancellationTokenSource(1000).Token);

            Assert.IsNotNull(dispatchedEvent);
            Assert.AreEqual(id, dispatchedEvent.Id);
            Assert.AreEqual("testValue", dispatchedEvent.TestValue);
        }

        public class TestAggregate
        {
            private readonly List<object> _uncommittedEvents = new List<object>();

            public TestAggregate()
            {
                Id = Guid.NewGuid();
            }

            [JsonConstructor]
            private TestAggregate(Guid id)
            {
                Id = id;
            }

            public Guid Id { get; }
            public string ConcurrencyToken { get; internal set; }
            public long Revision { get; internal set; }

            protected void Notify<TEvent>(TEvent evt)
            {
                if (evt == null)
                    throw new ArgumentNullException(nameof(evt));

                _uncommittedEvents.Add(evt);
            }

            internal IEnumerable<object> UncommittedEvents => _uncommittedEvents;

            internal void CommitEvents()
            {
                _uncommittedEvents.Clear();
            }

            public void TestOperation(string testValue)
            {
                Notify(new TestOperationDoneEvent(Id, testValue));
            }
        }

        public sealed class TestOperationDoneEvent
        {
            public TestOperationDoneEvent(Guid id, string testValue)
            {
                Id = id;
                TestValue = testValue;
            }

            public Guid Id { get; }

            public string TestValue { get; }
        }
    }
}
