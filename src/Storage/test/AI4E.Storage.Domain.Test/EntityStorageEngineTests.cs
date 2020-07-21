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
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Domain.Specification;
using AI4E.Storage.Domain.Specification.TestTypes;
using AI4E.Storage.MongoDB;
using AI4E.Storage.MongoDB.Test.Utils;
using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Xunit;

using CommitAttempt = AI4E.Storage.Domain.CommitAttempt<AI4E.Storage.Domain.Specification.TestCommitAttemptEntry>;

namespace AI4E.Storage.Domain.Test
{
    public sealed class EntityStorageEngineTests : EntityStorageEngineSpecification
    {
        private static readonly Type _domainEntityBaseType = typeof(DomainEntityBase);

        private readonly MongoClient _databaseClient = DatabaseRunner.CreateClient();
        private readonly Lazy<IDatabase> _database;

        public EntityStorageEngineTests()
        {
            _database = new Lazy<IDatabase>(BuildDatabase);

            Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            Fixture.Inject(new EntityIdentifier(_domainEntityBaseType, "a"));
            Fixture.Inject(new DomainEventCollection(new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEvent2), new DomainEvent2())
            }));

            Fixture.Inject<CancellationToken>(default);
            Fixture.Inject(Options.Create(new DomainStorageOptions { SynchronousEventDispatch = true }));

            Fixture.Register(() => _database.Value);
            Fixture.Register(() =>
            {
                var eventDispatcher = Fixture.Create<IDomainEventDispatcher>();

                return Create(eventDispatcher);
            });
        }

        private IFixture Fixture { get; }

        private IDatabase BuildDatabase()
        {
            var wrappedDatabase = _databaseClient.GetDatabase(DatabaseName.GenerateRandom());
            return new MongoDatabase(wrappedDatabase);
        }

        protected override IEntityStorageEngine Create(IDomainEventDispatcher eventDispatcher)
        {
            var database = Fixture.Create<IDatabase>();
            var optionsAccessor = Fixture.Create<IOptions<DomainStorageOptions>>();

            return new EntityStorageEngine(database, eventDispatcher, optionsAccessor);
        }

        [Fact]
        public void CtorNullDatabaseThrowsArgumentNullExceptionTest()
        {
            // Arrange
            // -

            // Act
            void Act()
            {
                new EntityStorageEngine(
                    database: null,
                    Fixture.Create<IDomainEventDispatcher>(),
                    Fixture.Create<IOptions<DomainStorageOptions>>());
            }

            // Assert
            Assert.Throws<ArgumentNullException>("database", Act);
        }

        [Fact]
        public void CtorNullEventDispatcherThrowsArgumentNullExceptionTest()
        {
            // Arrange
            // -

            // Act
            void Act()
            {
                new EntityStorageEngine(
                    Fixture.Create<IDatabase>(),
                    eventDispatcher: null,
                    Fixture.Create<IOptions<DomainStorageOptions>>());
            }

            // Assert
            Assert.Throws<ArgumentNullException>("eventDispatcher", Act);
        }

        [Fact]
        public void CtorNullOptionsAccessorThrowsArgumentNullExceptionTest()
        {
            // Arrange
            // -

            // Act
            void Act()
            {
                new EntityStorageEngine(
                    Fixture.Create<IDatabase>(),
                    Fixture.Create<IDomainEventDispatcher>(),
                    optionsAccessor: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("optionsAccessor", Act);
        }

        [Fact]
        public async Task CommitWithOtherEpochThanInCacheIsSuccessTest()
        {
            // Arrange

            // We want the database entry to stay alive but be marked as deleted, so that the epoch is increased.
            // Do never complete the event dispatch but do not block on this, otherwise we are lost here.
            Fixture.Inject(Options.Create(new DomainStorageOptions { SynchronousEventDispatch = false }));
            Fixture.Freeze<Mock<IDomainEventDispatcher>>()
               .Setup(eventDispatcher => eventDispatcher.DispatchAsync(
                   It.IsAny<DomainEvent>(), Fixture.Create<CancellationToken>()))
               .Returns(Task.Delay(Timeout.Infinite).AsValueTask());
            var subject = Fixture.Create<IEntityStorageEngine>();
            var testSubject = Fixture.Create<IEntityStorageEngine>();

            // Add an entry, query it in another instance to fill the cache and delete and recreate it in the original 
            // instance
            await subject.CommitAsync(CreateCommitAttempt, Fixture.Create<CancellationToken>());
            await testSubject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());
            await subject.CommitAsync(DeleteCommitAttempt, Fixture.Create<CancellationToken>());
            await subject.CommitAsync(CreateCommitAttempt, Fixture.Create<CancellationToken>());

            // testSubject now has in its cache an entry with epoch = 0 and revision = 1
            // In the database is an entry with               epoch = 1 and revision = 1

            // Act
            var commitResult = await testSubject.CommitAsync(DeleteCommitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitWithOtherEpochThanInCacheAndOtherRevisionIsFailureTest()
        {
            // Arrange

            // We want the database entry to stay alive but be marked as deleted, so that the epoch is increased.
            // Do never complete the event dispatch but do not block on this, otherwise we are lost here.
            Fixture.Inject(Options.Create(new DomainStorageOptions { SynchronousEventDispatch = false }));
            Fixture.Freeze<Mock<IDomainEventDispatcher>>()
               .Setup(eventDispatcher => eventDispatcher.DispatchAsync(
                   It.IsAny<DomainEvent>(), Fixture.Create<CancellationToken>()))
               .Returns(Task.Delay(Timeout.Infinite).AsValueTask());
            var subject = Fixture.Create<IEntityStorageEngine>();
            var testSubject = Fixture.Create<IEntityStorageEngine>();

            // Add an entry, query it in another instance to fill the cache and delete and recreate it in the original 
            // instance
            await subject.CommitAsync(CreateCommitAttempt, Fixture.Create<CancellationToken>());
            await testSubject.QueryEntityAsync(
                Fixture.Create<EntityIdentifier>(), bypassCache: false, Fixture.Create<CancellationToken>());
            await subject.CommitAsync(DeleteCommitAttempt, Fixture.Create<CancellationToken>());
            await subject.CommitAsync(CreateCommitAttempt, Fixture.Create<CancellationToken>());
            await subject.CommitAsync(UpdateCommitAttempt, Fixture.Create<CancellationToken>());

            // testSubject now has in its cache an entry with epoch = 0 and revision = 1
            // In the database is an entry with               epoch = 1 and revision = 2

            // Act
            var commitResult = await testSubject.CommitAsync(DeleteCommitAttempt, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.ConcurrencyFailure, commitResult);
        }

        // TODO: This is a copy. Use some helper to centrally configure these.
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
    }
}
