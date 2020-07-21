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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Domain.Specification.TestTypes;
using AI4E.Utils;
using AutoFixture;
using AutoFixture.AutoMoq;
using Moq;
using Xunit;

using CommitAttempt = AI4E.Storage.Domain.CommitAttempt<AI4E.Storage.Domain.Specification.TestCommitAttemptEntry>;
using CommitAttemptEntryEqualityComparer = AI4E.Storage.Domain.Specification.CommitAttemptEntryEqualityComparer<AI4E.Storage.Domain.Specification.TestCommitAttemptEntry>;

namespace AI4E.Storage.Domain.Specification
{
    public abstract class EntityStorageEngineSpecification
    {
        private static readonly Type _domainEntityBaseType = typeof(DomainEntityBase);

        private IFixture Fixture { get; }

        public EntityStorageEngineSpecification()
        {
            Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            Fixture.Inject(new EntityIdentifier(_domainEntityBaseType, "a"));
            Fixture.Inject(new DomainEventCollection(new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEvent2), new DomainEvent2())
            }));

            Fixture.Inject<CancellationToken>(default);

            Fixture.Register(() =>
            {
                var eventDispatcher = Fixture.Create<IDomainEventDispatcher>();

                return Create(eventDispatcher);
            });
        }

        private static CommitAttempt EmptyCommitAttempt => new CommitAttempt(default);

        private CommitAttempt CreateCommitAttempt
        {
            get
            {
                var commitAttemptEntry = new TestCommitAttemptEntry
                {
                    EntityIdentifier = Fixture.Create<EntityIdentifier>(),
                    Operation = CommitOperation.Store,
                    Revision = 1,
                    ConcurrencyToken = Fixture.Create<ConcurrencyToken>(),
                    DomainEvents = Fixture.Create<DomainEventCollection>(),
                    ExpectedRevision = 0,
                    Entity = new DomainEntity1()
                };

                return new CommitAttempt(
                    new CommitAttemptEntryCollection<TestCommitAttemptEntry>(
                        new[] { commitAttemptEntry }.ToImmutableArray()));
            }
        }

        private CommitAttempt AppendEventsOnlyCommitAttempt
        {
            get
            {
                var commitAttemptEntry = new TestCommitAttemptEntry
                {
                    EntityIdentifier = Fixture.Create<EntityIdentifier>(),
                    Operation = CommitOperation.AppendEventsOnly,
                    Revision = 0,
                    ConcurrencyToken = default,
                    DomainEvents = Fixture.Create<DomainEventCollection>(),
                    ExpectedRevision = 0,
                    Entity = null
                };

                return new CommitAttempt(
                    new CommitAttemptEntryCollection<TestCommitAttemptEntry>(
                        new[] { commitAttemptEntry }.ToImmutableArray()));
            }
        }

        private CommitAttempt UpdateCommitAttempt
        {
            get
            {
                var commitAttemptEntry = new TestCommitAttemptEntry
                {
                    EntityIdentifier = Fixture.Create<EntityIdentifier>(),
                    Operation = CommitOperation.Store,
                    Revision = 2,
                    ConcurrencyToken = Fixture.Create<ConcurrencyToken>(),
                    DomainEvents = Fixture.Create<DomainEventCollection>(),
                    ExpectedRevision = 1,
                    Entity = new DomainEntity1()
                };

                return new CommitAttempt(
                    new CommitAttemptEntryCollection<TestCommitAttemptEntry>(
                        new[] { commitAttemptEntry }.ToImmutableArray()));
            }
        }

        private CommitAttempt DeleteCommitAttempt
        {
            get
            {
                var commitAttemptEntry = new TestCommitAttemptEntry
                {
                    EntityIdentifier = Fixture.Create<EntityIdentifier>(),
                    Operation = CommitOperation.Delete,
                    Revision = 0,
                    ConcurrencyToken = default,
                    DomainEvents = Fixture.Create<DomainEventCollection>(),
                    ExpectedRevision = 1,
                    Entity = new DomainEntity1()
                };

                return new CommitAttempt(
                    new CommitAttemptEntryCollection<TestCommitAttemptEntry>(
                        new[] { commitAttemptEntry }.ToImmutableArray()));
            }
        }

        private async Task SeedAsync(IEntityStorageEngine entityStorageEngine)
        {
            await entityStorageEngine.CommitAsync(CreateCommitAttempt, Fixture.Create<CancellationToken>());
        }

        protected abstract IEntityStorageEngine Create(IDomainEventDispatcher eventDispatcher);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task QueryEntityAsyncDefaultEntityIdentifierReturnsNotFoundResultTest(bool bypassCache)
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            var result = await subject.QueryEntityAsync(
                entityIdentifier: default, bypassCache, Fixture.Create<CancellationToken>());

            // Assert
            Assert.False(result.IsFound());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task QueryEntityAsyncEmptyDatabaseReturnsNotFoundResultTest(bool bypassCache)
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            var result = await subject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache, Fixture.Create<CancellationToken>());

            // Assert
            Assert.False(result.IsFound());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task QueryEntitiesAsyncNullEntityTypeThrowsArgumentNullExceptionTest(bool bypassCache)
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            async Task Act()
            {
                await subject.QueryEntitiesAsync(
                    entityType: null, bypassCache, Fixture.Create<CancellationToken>()).ToListAsync();
            }

            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>("entityType", Act);
        }

        [Theory]
        [InlineData(typeof(Action), false)]
        [InlineData(typeof(TestStruct), false)]
        [InlineData(typeof(IDomainEntity), false)]
        [InlineData(typeof(DomainEntity<>), false)]
        [InlineData(typeof(Action), true)]
        [InlineData(typeof(TestStruct), true)]
        [InlineData(typeof(IDomainEntity), true)]
        [InlineData(typeof(DomainEntity<>), true)]
        public async Task QueryEntitiesAsyncIllegalEntityTypeThrowsArgumentExceptionTest(
            Type entityType, bool bypassCache)
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            async Task Act()
            {
                await subject.QueryEntitiesAsync(
                    entityType, bypassCache, Fixture.Create<CancellationToken>()).ToListAsync();
            }

            // Assert
            await Assert.ThrowsAsync<ArgumentException>("entityType", Act);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task QueryEntitiesAsyncEmptyDatabaseYieldsNoResultsTest(bool bypassCache)
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            var results = await subject.QueryEntitiesAsync(
                _domainEntityBaseType, bypassCache, Fixture.Create<CancellationToken>()).ToListAsync();

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task CommitAsyncEmptyCommitAttemptIsSuccessWhenDatabaseIsEmptyTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = EmptyCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncCreateCommitAttemptIsSuccessWhenDatabaseIsEmptyTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = CreateCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncAppendEventsOnlyCommitAttemptIsSuccessWhenDatabaseIsEmptyTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = AppendEventsOnlyCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncCreateCommitAttemptUpdatesCacheTest()
        {
            // Arrange
            var commitAttempt = CreateCommitAttempt;
            var subject = Fixture.Create<IEntityStorageEngine>();
            await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Act
            var result = await subject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.True(result.LoadedFromCache);
        }

        [Fact]
        public async Task CommitAsyncCreateCommitAttemptUpdatesDatabaseTest()
        {
            // Arrange
            var commitAttempt = CreateCommitAttempt;
            await Fixture.Create<IEntityStorageEngine>().CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            var subject = Fixture.Create<IEntityStorageEngine>();
            // Act
            var result = await subject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.False(result.LoadedFromCache);
        }

        [Fact]
        public async Task CommitAsyncUpdateCommitAttemptIsFailureWhenDatabaseIsEmptyTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = UpdateCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.ConcurrencyFailure, commitResult);
        }

        [Fact]
        public async Task CommitAsyncDeleteCommitAttemptIsFailureWhenDatabaseIsEmptyTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = DeleteCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.ConcurrencyFailure, commitResult);
        }

        [Fact]
        public async Task CommitAsyncUpdateCommitAttemptIsSuccessWhenEntityInCacheMatchesExpectedRevisionTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            await SeedAsync(subject);
            var commitAttempt = UpdateCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncDeleteCommitAttemptIsSuccessWhenEntityInCacheMatchesExpectedRevisionTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            await SeedAsync(subject);
            var commitAttempt = DeleteCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncAppendEventsOnlyCommitAttemptIsSuccessWhenEntityInCacheMatchesExpectedRevisionTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            await SeedAsync(subject);
            var commitAttempt = AppendEventsOnlyCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncCreateCommitAttemptIsFailureWhenEntityInCacheDoesNotMatchExpectedRevisionTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorageEngine>();
            await SeedAsync(subject);
            var commitAttempt = CreateCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.ConcurrencyFailure, commitResult);
        }

        [Fact]
        public async Task CommitAsyncUpdateCommitAttemptIsSuccessWhenEntityInDatabaseMatchesExpectedRevisionTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = UpdateCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncDeleteCommitAttemptIsSuccessWhenEntityInDatabaseMatchesExpectedRevisionTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = DeleteCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncAppendEventsOnlyCommitAttemptIsSuccessWhenEntityInDatabaseMatchesExpectedRevisionTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = AppendEventsOnlyCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncCreateCommitAttemptIsFailureWhenEntityInDatabaseDoesNotMatcheExpectedRevisionTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = CreateCommitAttempt;

            // Act
            var commitResult = await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.ConcurrencyFailure, commitResult);
        }

        [Fact]
        public async Task CommitAsyncUpdateCommitAttemptUpdatesCacheTest()
        {
            // Arrange
            var commitAttempt = UpdateCommitAttempt;
            var subject = Fixture.Create<IEntityStorageEngine>();
            await SeedAsync(subject);

            await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Act
            var result = await subject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.True(result.LoadedFromCache);
        }

        [Fact]
        public async Task CommitAsyncUpdateCommitAttemptUpdatesDatabaseTest()
        {
            // Arrange
            var commitAttempt = UpdateCommitAttempt;
            var engine = Fixture.Create<IEntityStorageEngine>();
            await SeedAsync(engine);
            await engine.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            var result = await subject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.False(result.LoadedFromCache);
        }

        [Fact]
        public async Task CommitAsyncDeleteCommitAttemptUpdatesCacheTest()
        {
            // Arrange
            var commitAttempt = DeleteCommitAttempt;
            var subject = Fixture.Create<IEntityStorageEngine>();
            await SeedAsync(subject);

            await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Act
            var result = await subject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.False(result.IsFound());
            Assert.True(result.LoadedFromCache);
        }

        [Fact]
        public async Task CommitAsyncDeleteCommitAttemptUpdatesDatabaseTest()
        {
            // Arrange
            var commitAttempt = DeleteCommitAttempt;
            var engine = Fixture.Create<IEntityStorageEngine>();
            await SeedAsync(engine);
            await engine.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            var result = await subject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.False(result.IsFound());
            Assert.False(result.LoadedFromCache);
        }

        [Fact]
        public async Task CommitAsyncDispatchesDomainEventsTest()
        {
            // Arrange
            var dispatchedDomainEvents = new List<DomainEvent>();
            Fixture.Freeze<Mock<IDomainEventDispatcher>>()
                .Setup(eventDispatcher => eventDispatcher.DispatchAsync(
                    It.IsAny<DomainEvent>(), Fixture.Create<CancellationToken>()))
                .Callback<DomainEvent, CancellationToken>((domainEvent, _) => dispatchedDomainEvents.Add(domainEvent));

            var subject = Fixture.Create<IEntityStorageEngine>();
            var commitAttempt = AppendEventsOnlyCommitAttempt;

            // Act
            await subject.CommitAsync(commitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(Fixture.Create<DomainEventCollection>(), new DomainEventCollection(dispatchedDomainEvents));
        }

        [Fact]
        public async Task DomainEventsAreDispatchedAfterBreakdownTest()
        {
            // Arrange
            var commitAttempt = AppendEventsOnlyCommitAttempt;
            var commitCancellationSource = new CancellationTokenSource();

            Fixture.Freeze<Mock<IDomainEventDispatcher>>()
                .Setup(eventDispatcher => eventDispatcher.DispatchAsync(
                    It.IsAny<DomainEvent>(), Fixture.Create<CancellationToken>()))
                .Callback<DomainEvent, CancellationToken>((_1, _2) => commitCancellationSource.Cancel())
                .Returns(Task.Delay(Timeout.Infinite).AsValueTask());

            try
            {
                await Fixture.Create<IEntityStorageEngine>().CommitAsync(
                       commitAttempt, commitCancellationSource.Token);
            }
            catch (OperationCanceledException) { }

            var dispatchedDomainEvents = new List<DomainEvent>();
            var tcs = new TaskCompletionSource<object?>();
            var domainEventCount = commitAttempt.Entries.Select(p => p.DomainEvents.Count).Sum();

            void NotifyDomainEventDispatched(DomainEvent domainEvent, CancellationToken _)
            {
                bool domainEventsComplete;
                lock (dispatchedDomainEvents)
                {
                    dispatchedDomainEvents.Add(domainEvent);
                    domainEventsComplete = dispatchedDomainEvents.Count == domainEventCount;
                }

                if(domainEventsComplete)
                {
                    tcs.TrySetResult(null);
                }
            }

            Fixture.Freeze<Mock<IDomainEventDispatcher>>()
                .Setup(eventDispatcher => eventDispatcher.DispatchAsync(
                    It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
                .Callback<DomainEvent, CancellationToken>(NotifyDomainEventDispatched)
                .Returns(new ValueTask());


            // Act
            Fixture.Create<IEntityStorageEngine>();
            await tcs.Task.WithCancellation(new CancellationTokenSource(10000).Token);

            // Assert
            Assert.Equal(domainEventCount, dispatchedDomainEvents.Count);
        }

        [Fact]
        public async Task QueryEntityAsyncReadFromDatabaseTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            var result = await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.False(result.LoadedFromCache);
        }

        [Fact]
        public async Task QueryEntitiesAsyncReadFromDatabaseTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();

            // Act
            var results = await subject.QueryEntitiesAsync(
                 _domainEntityBaseType, bypassCache: false, Fixture.Create<CancellationToken>()).ToListAsync();

            // Assert
            Assert.Collection(results, result =>
            {
                Assert.True(result.IsFound());
                Assert.False(result.LoadedFromCache);
            });
        }

        [Fact]
        public async Task QueryEntityAsyncReadFromCacheTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: true, Fixture.Create<CancellationToken>());

            // Act
            var result = await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.True(result.LoadedFromCache);
        }

        [Fact]
        public async Task QueryEntitiesAsyncReadFromCacheTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: true, Fixture.Create<CancellationToken>());

            // Act
            var results = await subject.QueryEntitiesAsync(
                 _domainEntityBaseType, bypassCache: false, Fixture.Create<CancellationToken>()).ToListAsync();

            // Assert
            Assert.Collection(results, result =>
            {
                Assert.True(result.IsFound());
            });
        }

        [Fact]
        public async Task QueryEntityAsyncBypassCacheTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: true, Fixture.Create<CancellationToken>());

            await Fixture.Create<IEntityStorageEngine>().CommitAsync(
                UpdateCommitAttempt, Fixture.Create<CancellationToken>());

            // Act
            var result = await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: true, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.False(result.LoadedFromCache);
            Assert.Equal(2, result.Revision);
        }

        [Fact]
        public async Task QueryEntitiesAsyncBypassCacheTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: true, Fixture.Create<CancellationToken>());

            await Fixture.Create<IEntityStorageEngine>().CommitAsync(
               UpdateCommitAttempt, Fixture.Create<CancellationToken>());

            // Act
            var results = await subject.QueryEntitiesAsync(
                 _domainEntityBaseType, bypassCache: true, Fixture.Create<CancellationToken>()).ToListAsync();

            // Assert
            Assert.Collection(results, result =>
            {
                Assert.True(result.IsFound());
                Assert.False(result.LoadedFromCache);
                Assert.Equal(2, result.Revision);
            });
        }

        [Fact]
        public async Task QueryEntityAsyncUpdateCacheTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: true, Fixture.Create<CancellationToken>());

            await Fixture.Create<IEntityStorageEngine>().CommitAsync(
                UpdateCommitAttempt, Fixture.Create<CancellationToken>());

            await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: true, Fixture.Create<CancellationToken>());

            // Act
            var result = await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.True(result.LoadedFromCache);
            Assert.Equal(2, result.Revision);
        }

        [Fact]
        public async Task QueryEntitiesAsyncUpdateCacheTest()
        {
            // Arrange
            await SeedAsync(Fixture.Create<IEntityStorageEngine>());
            var subject = Fixture.Create<IEntityStorageEngine>();
            await subject.QueryEntityAsync(
                 Fixture.Create<EntityIdentifier>(), bypassCache: true, Fixture.Create<CancellationToken>());

            await Fixture.Create<IEntityStorageEngine>().CommitAsync(
               UpdateCommitAttempt, Fixture.Create<CancellationToken>());

            await subject.QueryEntitiesAsync(
                 _domainEntityBaseType, bypassCache: true, Fixture.Create<CancellationToken>()).ToListAsync();

            // Act
            var result = await subject.QueryEntityAsync(
                  Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());

            // Assert
            Assert.True(result.IsFound());
            Assert.True(result.LoadedFromCache);
            Assert.Equal(2, result.Revision);
        }
    }

    // TODO: This should be read-only.
    public sealed class TestCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TestCommitAttemptEntry>
    {
        private static readonly ThreadLocal<CommitAttemptEntryEqualityComparer> _comparers
            = new ThreadLocal<CommitAttemptEntryEqualityComparer>(BuildComparer, trackAllValues: false);

        private static CommitAttemptEntryEqualityComparer BuildComparer()
        {
            return new CommitAttemptEntryEqualityComparer(CommitAttemptEntryEquality.All);
        }

        public EntityIdentifier EntityIdentifier { get; set; }

        public CommitOperation Operation { get; set; }

        public long Revision { get; set; }

        public ConcurrencyToken ConcurrencyToken { get; set; }

        public DomainEventCollection DomainEvents { get; set; }

        public long ExpectedRevision { get; set; }

        public object? Entity { get; set; }

        public bool Equals(TestCommitAttemptEntry? other)
        {
            if (other is null)
                return false;

            var comparer = _comparers.Value!;
            return comparer.Equals(this, other);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TestCommitAttemptEntry);
        }

        public override int GetHashCode()
        {
            var comparer = _comparers.Value!;
            return comparer.GetHashCode(this);
        }
    }

    internal sealed class CommitAttemptEntryEqualityComparer<TCommitAttemptEntry> : IEqualityComparer<TCommitAttemptEntry>
        where TCommitAttemptEntry : ICommitAttemptEntry
    {
        private readonly CommitAttemptEntryEquality _equalityOptions;

        public CommitAttemptEntryEqualityComparer(CommitAttemptEntryEquality equalityOptions)
        {
            _equalityOptions = equalityOptions;
        }

        public bool Equals([AllowNull] TCommitAttemptEntry x, [AllowNull] TCommitAttemptEntry y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null)
                return false;

            Debug.Assert(y != null);

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.EntityIdentifier)
                && x.EntityIdentifier != y.EntityIdentifier)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.Operation)
                && x.Operation != y.Operation)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.Revision)
                && x.Revision != y.Revision)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.ConcurrencyToken)
                && x.ConcurrencyToken != y.ConcurrencyToken)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.DomainEvents)
                && x.DomainEvents != y.DomainEvents)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.ExpectedRevision)
                && x.ExpectedRevision != y.ExpectedRevision)
            {
                return false;
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.Entity)
                && x.Entity != y.Entity)
            {
                return false;
            }

            return true;
        }

        public int GetHashCode([DisallowNull] TCommitAttemptEntry obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            var hashCode = new HashCode();

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.EntityIdentifier))
            {
                hashCode.Add(obj.EntityIdentifier);
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.Operation))
            {
                hashCode.Add(obj.Operation);
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.Revision))
            {
                hashCode.Add(obj.Revision);
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.ConcurrencyToken))
            {
                hashCode.Add(obj.ConcurrencyToken);
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.DomainEvents))
            {
                hashCode.Add(obj.DomainEvents);
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.ExpectedRevision))
            {
                hashCode.Add(obj.ExpectedRevision);
            }

            if (_equalityOptions.IncludesFlag(CommitAttemptEntryEquality.Entity))
            {
                hashCode.Add(obj.Entity);
            }

            return hashCode.ToHashCode();
        }
    }

    [Flags]
    internal enum CommitAttemptEntryEquality
    {
        None = 0,
        EntityIdentifier = 0x01,
        Operation = 0x02,
        Revision = 0x04,
        ConcurrencyToken = 0x08,
        DomainEvents = 0x10,
        ExpectedRevision = 0x20,
        Entity = 0x40,
        All = 0x7F
    }
}
