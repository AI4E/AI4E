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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Domain.Specification.Helpers;
using AI4E.Storage.Domain.Specification.TestTypes;
using AutoFixture;
using AutoFixture.AutoMoq;
using Moq;
using Xunit;

namespace AI4E.Storage.Domain.Specification
{
    public abstract class EntityStorageSpecification
    {
        private static readonly Type _domainEntityBaseType = typeof(DomainEntityBase);

        protected EntityStorageSpecification()
        {
            Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

            Fixture.Inject<IEntityMetadataManager>(new EntityMetadataManager()); // TODO: Can we get rid of this dependency?
            Fixture.Inject(new EntityIdentifier(_domainEntityBaseType, "a"));
            Fixture.Inject<IFoundEntityQueryResult>(new FoundEntityQueryResult(Fixture.Create<EntityIdentifier>(), new DomainEntity1(), new ConcurrencyToken("xxx"), revision: 22, loadedFromCache: true, scope: EntityQueryResultGlobalScope.Instance));
            Fixture.Inject<IEntityQueryResult>(new NotFoundEntityQueryResult(EntityIdentifier, loadedFromCache: false, EntityQueryResultGlobalScope.Instance));
            Fixture.Inject(new DomainEventCollection(new[]
            {
                new DomainEvent(typeof(DomainEventBase), new DomainEvent1()),
                new DomainEvent(typeof(DomainEvent2), new DomainEvent2())
            }));

            Fixture.Freeze<Mock<IEntityStorageEngine>>();
            Fixture.Freeze<Mock<IDomainQueryProcessor>>();
            Fixture.Freeze<CancellationToken>();

            Fixture.Register(() =>
            {
                var storageEngine = Fixture.Create<IEntityStorageEngine>();
                var metadataManager = Fixture.Create<IEntityMetadataManager>();

                return Create(storageEngine, metadataManager);
            });

            Fixture.Freeze<IEntityStorage>();
        }

        private IFixture Fixture { get; }

        private EntityIdentifier EntityIdentifier => Fixture.Create<EntityIdentifier>();

        private IFoundEntityQueryResult FoundResult => Fixture.Create<IFoundEntityQueryResult>();

        private IEntityQueryResult NotFoundResult => Fixture.Create<IEntityQueryResult>();

        private DomainEventCollection DomainEvents => Fixture.Create<DomainEventCollection>();

        protected abstract IEntityStorage Create(IEntityStorageEngine storageEngine, IEntityMetadataManager metadataManager);

        #region Helpers

        private void EngineReturnsNoResult()
        {
            Fixture.Create<Mock<IEntityStorageEngine>>()
                .Setup(storageEngine => storageEngine.QueryEntitiesAsync(
                    _domainEntityBaseType, false, Fixture.Create<CancellationToken>()))
                .Returns(AsyncEnumerable.Empty<IFoundEntityQueryResult>());

            Fixture.Create<Mock<IEntityStorageEngine>>()
                .Setup(storageEngine => storageEngine.QueryEntityAsync(
                    EntityIdentifier, It.IsAny<bool>(), Fixture.Create<CancellationToken>()))
                .ReturnsAsync(NotFoundResult);
        }

        private void EngineReturnsOneResult()
        {
            Fixture.Create<Mock<IEntityStorageEngine>>()
               .Setup(storageEngine => storageEngine.QueryEntitiesAsync(
                   _domainEntityBaseType, It.IsAny<bool>(), Fixture.Create<CancellationToken>()))
                .Returns(FoundResult.Yield().ToAsyncEnumerable());

            Fixture.Create<Mock<IEntityStorageEngine>>()
                .Setup(storageEngine => storageEngine.QueryEntityAsync(
                    EntityIdentifier, It.IsAny<bool>(), Fixture.Create<CancellationToken>()))
                .ReturnsAsync(FoundResult);
        }

        private void EngineReturnsMultipleResults()
        {
            EngineReturnsOneResult();

            var results = new IFoundEntityQueryResult[]
            {
                new FoundEntityQueryResult(new EntityIdentifier(typeof(DomainEntityBase), "1"), new DomainEntity1(), new ConcurrencyToken("xxx"), revision: 22, loadedFromCache: false, scope: EntityQueryResultGlobalScope.Instance),
                new FoundEntityQueryResult(new EntityIdentifier(typeof(DomainEntityBase), "2"), new DomainEntity2(), new ConcurrencyToken("ggf"), revision: 1, loadedFromCache: false, scope: EntityQueryResultGlobalScope.Instance),
                new FoundEntityQueryResult(new EntityIdentifier(typeof(DomainEntityBase), "3"), new DomainEntity1(), new ConcurrencyToken("hgngn"), revision: 5, loadedFromCache: true, scope: EntityQueryResultGlobalScope.Instance)
            }.ToImmutableList();

            Fixture.Create<Mock<IEntityStorageEngine>>()
                .Setup(storageEngine => storageEngine.QueryEntitiesAsync(
                    _domainEntityBaseType, false, Fixture.Create<CancellationToken>()))
                .Returns(results.ToAsyncEnumerable());
        }

        private async Task<DomainEntity1> StoreEntityAsync(DomainEntity1? entity = null)
        {
            var subject = Fixture.Create<IEntityStorage>();
            entity ??= new DomainEntity1();
            var entityDescriptor = new EntityDescriptor(EntityIdentifier.EntityType, entity);

            subject.MetadataManager.SetId(entityDescriptor, FoundResult.EntityIdentifier.EntityId);
            subject.MetadataManager.SetConcurrencyToken(entityDescriptor, FoundResult.ConcurrencyToken);
            subject.MetadataManager.SetRevision(entityDescriptor, FoundResult.Revision);

            foreach (var domainEvent in DomainEvents)
            {
                subject.MetadataManager.AddEvent(entityDescriptor, domainEvent);
            }

            await subject.StoreAsync(entityDescriptor, Fixture.Create<CancellationToken>());

            return entity;
        }

        private async Task DeleteEntityAsync(DomainEntity1? entity = null)
        {
            var subject = Fixture.Create<IEntityStorage>();
            entity ??= new DomainEntity1();
            var entityDescriptor = new EntityDescriptor(_domainEntityBaseType, entity);

            subject.MetadataManager.SetId(entityDescriptor, FoundResult.EntityIdentifier.EntityId);
            subject.MetadataManager.SetConcurrencyToken(entityDescriptor, FoundResult.ConcurrencyToken);
            subject.MetadataManager.SetRevision(entityDescriptor, FoundResult.Revision);

            await subject.DeleteAsync(entityDescriptor, Fixture.Create<CancellationToken>());
        }

        private void ObserveCommitAttempt(Action<object> observer)
        {
            Fixture.Create<Mock<IEntityStorageEngine>>()
                .Setup(storageEngine => storageEngine.ProcessCommitAttemptAsync(
                    It.Is<CommitAttempt<CommitAttemptEntryTypeMatcher>>((o, t) => true), Fixture.Create<CancellationToken>()))
                .Callback<object, CancellationToken>(
                    (commitAttempt, cancellation) => { observer(commitAttempt); })
                .ReturnsAsync(EntityCommitResult.ConcurrencyFailure);
        }

        private void ObserveQueryProcessor(Action<EntityIdentifier, IDomainQueryExecutor> observer)
        {
            Fixture.Create<Mock<IDomainQueryProcessor>>()
              .Setup(queryProcessor => queryProcessor.ProcessAsync(
                  It.IsAny<EntityIdentifier>(), It.IsAny<IDomainQueryExecutor>(), Fixture.Create<CancellationToken>()))
              .Callback<EntityIdentifier, IDomainQueryExecutor, CancellationToken>(
                  (entityIdentifier, executor, cancellation) => { observer(entityIdentifier, executor); })
              .ReturnsAsync(FoundResult);
        }

        #endregion

        [Fact]
        public void EntityMetadataManagerIsSetCorrectlyTest()
        {
            // Arrange
            // -

            // Act
            var subject = Fixture.Create<IEntityStorage>();

            // Assert
            Assert.Same(Fixture.Create<IEntityMetadataManager>(), subject.MetadataManager);
        }

        [Fact]
        public void LoadedEntitiesIsEmptyOnNewInstanceTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorage>();

            // Act
            var loadedEntities = subject.LoadedEntities.ToList();

            // Assert
            Assert.Empty(loadedEntities);
        }

        [Fact]
        public async Task LoadEntitiesAsyncNullEntityTypeThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorage>();

            // Act
            async Task Act()
            {
                await subject.LoadEntitiesAsync(entityType: null, Fixture.Create<CancellationToken>()).ToListAsync();
            }

            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>("entityType", Act);
        }

        [Theory]
        [InlineData(typeof(Action))]
        [InlineData(typeof(TestStruct))]
        [InlineData(typeof(IDomainEntity))]
        [InlineData(typeof(DomainEntity<>))]
        public async Task LoadEntitiesAsyncIllegalEntityTypeThrowsArgumentExceptionTest(Type entityType)
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorage>();

            // Act
            async Task Act()
            {
                await subject.LoadEntitiesAsync(entityType, Fixture.Create<CancellationToken>()).ToListAsync();
            }

            // Assert
            await Assert.ThrowsAsync<ArgumentException>("entityType", Act);
        }

        [Fact]
        public async Task LoadEntitiesAsyncCorrectlyCallsStorageEngineTest()
        {
            // Arrange 
            EngineReturnsNoResult();
            var subject = Fixture.Create<IEntityStorage>();

            // Act
            await subject.LoadEntitiesAsync(_domainEntityBaseType, Fixture.Create<CancellationToken>()).ToListAsync();

            // Assert
            Fixture.Create<Mock<IEntityStorageEngine>>()
                .Verify(storageEngine => storageEngine.QueryEntitiesAsync(
                    _domainEntityBaseType, false, Fixture.Create<CancellationToken>()));
        }

        [Fact]
        public async Task LoadEntitiesAsyncReturnsCorrectNumberOfResultsTest()
        {
            // Arrange
            EngineReturnsMultipleResults();

            var subject = Fixture.Create<IEntityStorage>();

            // Act
            var loadResults = await subject.LoadEntitiesAsync(
                _domainEntityBaseType, Fixture.Create<CancellationToken>()).ToListAsync();

            // Assert
            Assert.Equal(3, loadResults.Count);
        }

        [Fact]
        public async Task LoadEntitiesAsyncScopesResultTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();

            // Act
            var loadResult = await subject.LoadEntitiesAsync(_domainEntityBaseType, Fixture.Create<CancellationToken>()).FirstOrDefaultAsync();

            // Assert
            Assert.NotSame(FoundResult.Entity, loadResult.Entity);
        }

        [Fact]
        public async Task LoadEntitiesAsyncRegistersResultInLoadedEntitiesTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();
            await subject.LoadEntitiesAsync(_domainEntityBaseType, Fixture.Create<CancellationToken>()).FirstOrDefaultAsync();

            // Act
            var loadResults = subject.LoadedEntities.ToList();

            // Assert
            Assert.Single(loadResults);
        }

        [Fact]
        public async Task LoadEntitiesAsyncRepeatableReadTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();
            var expectedLoadResult = await subject.LoadEntitiesAsync(_domainEntityBaseType, Fixture.Create<CancellationToken>()).FirstOrDefaultAsync();

            EngineReturnsMultipleResults();

            // Act
            var loadResults = await subject.LoadEntitiesAsync(_domainEntityBaseType, Fixture.Create<CancellationToken>()).ToListAsync();

            // Assert
            Assert.Collection(loadResults, loadResult => Assert.Same(expectedLoadResult, loadResult));
        }

        [Fact]
        public async Task LoadEntitiesAsyncReflectsStoredEntityTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var entity = await StoreEntityAsync();
            var subject = Fixture.Create<IEntityStorage>();

            // Act
            var loadResult = await subject.LoadEntitiesAsync(_domainEntityBaseType, Fixture.Create<CancellationToken>()).FirstOrDefaultAsync();

            // Assert
            Assert.Same(entity, loadResult.Entity);
        }

        [Fact]
        public async Task LoadEntitiesAsyncReflectsDeletedEntityTest()
        {
            // Arrange
            EngineReturnsOneResult();

            await DeleteEntityAsync();
            var subject = Fixture.Create<IEntityStorage>();

            // Act
            var loadResults = await subject.LoadEntitiesAsync(_domainEntityBaseType, Fixture.Create<CancellationToken>()).ToListAsync();

            // Assert
            Assert.Empty(loadResults);
        }

        [Fact]
        public async Task LoadEntitiesAsyncSetsEntityMetadataTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();

            // Act
            var loadResult = await subject.LoadEntitiesAsync(_domainEntityBaseType, Fixture.Create<CancellationToken>()).FirstOrDefaultAsync();

            // Assert
            Assert.Equal(loadResult.EntityIdentifier.EntityId, subject.MetadataManager.GetId(new EntityDescriptor(_domainEntityBaseType, loadResult.Entity)));
            Assert.Equal(loadResult.ConcurrencyToken, subject.MetadataManager.GetConcurrencyToken(new EntityDescriptor(_domainEntityBaseType, loadResult.Entity)));
            Assert.Equal(loadResult.Revision, subject.MetadataManager.GetRevision(new EntityDescriptor(_domainEntityBaseType, loadResult.Entity)));
            Assert.Empty(subject.MetadataManager.GetUncommittedEvents(new EntityDescriptor(_domainEntityBaseType, loadResult.Entity)));
        }

        [Fact]
        public async Task LoadEntityAsyncNullQueryProcessorThrowsArgumentNullExceptionTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorage>();

            // Act
            async Task Act()
            {
                await subject.LoadEntityAsync(EntityIdentifier, queryProcessor: null, Fixture.Create<CancellationToken>());
            }

            // Assert
            await Assert.ThrowsAsync<ArgumentNullException>("queryProcessor", Act);
        }

        [Fact]
        public async Task LoadEntityAsyncCallsQueryProcessorOnceTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorage>();

            Fixture.Create<Mock<IDomainQueryProcessor>>()
                .Setup(queryProcessor => queryProcessor.ProcessAsync(
                    It.IsAny<EntityIdentifier>(), It.IsAny<IDomainQueryExecutor>(), Fixture.Create<CancellationToken>()))
                .ReturnsAsync(FoundResult);

            // Act
            await subject.LoadEntityAsync(EntityIdentifier, Fixture.Create<IDomainQueryProcessor>(), Fixture.Create<CancellationToken>());

            // Assert
            Fixture.Create<Mock<IDomainQueryProcessor>>().Verify(queryProcessor => queryProcessor.ProcessAsync(
                It.IsAny<EntityIdentifier>(),
                It.IsAny<IDomainQueryExecutor>(),
                Fixture.Create<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task LoadEntityAsyncCallsQueryProcessorWithCorrectEntityIdentifierTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorage>();
            EntityIdentifier entityIdentifier;
            ObserveQueryProcessor((observedEntityIdentifier, _) => entityIdentifier = observedEntityIdentifier);

            // Act
            await subject.LoadEntityAsync(EntityIdentifier, Fixture.Create<IDomainQueryProcessor>(), Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityIdentifier, entityIdentifier);
        }

        [Fact]
        public async Task LoadEntityAsyncCallsQueryProcessorWithNonNullExecutorTest()
        {
            // Arrange         
            IDomainQueryExecutor? executor = null;
            ObserveQueryProcessor((_, observedExecutor) => executor = observedExecutor);
            var subject = Fixture.Create<IEntityStorage>();

            // Act
            await subject.LoadEntityAsync(EntityIdentifier, Fixture.Create<IDomainQueryProcessor>(), Fixture.Create<CancellationToken>());

            // Assert
            Assert.NotNull(executor);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LoadEntityAsyncCorrectlyCallsStorageEngineTest(bool bypassCache)
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();
            var queryProcessor = new TestDomainQueryProcessor { BypassCache = bypassCache };

            // Act
            await subject.LoadEntityAsync(EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            // Assert
            Fixture.Create<Mock<IEntityStorageEngine>>()
                .Verify(storageEngine => storageEngine.QueryEntityAsync(
                    EntityIdentifier, bypassCache, Fixture.Create<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task LoadEntityAsyncScopesResultTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();
            var queryProcessor = new TestDomainQueryProcessor();

            // Act
            var loadResult = (IFoundEntityQueryResult)await subject.LoadEntityAsync(
                EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            // Assert
            Assert.NotSame(FoundResult.Entity, loadResult.Entity);
        }

        [Fact]
        public async Task LoadEntityAsyncRegistersTrackableResultInLoadedEntitiesTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();
            var queryProcessor = new TestDomainQueryProcessor();
            await subject.LoadEntityAsync(EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            // Act
            var loadResults = subject.LoadedEntities.ToList();

            // Assert
            Assert.Single(loadResults);
        }

        [Fact]
        public async Task LoadEntityAsyncDoesNotRegisterNonTrackableResultInLoadedEntitiesTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();
            var queryProcessor = new TestDomainQueryProcessor
            {
                Result = new UnexpectedRevisionEntityVerificationResult(EntityIdentifier)
            };
            await subject.LoadEntityAsync(EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            // Act
            var loadResults = subject.LoadedEntities.ToList();

            // Assert
            Assert.Empty(loadResults);
        }

        [Fact]
        public async Task LoadEntityAsyncRepeatableReadTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();
            var queryProcessor = new TestDomainQueryProcessor();

            var expectedLoadResult = await subject.LoadEntityAsync(EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            EngineReturnsNoResult();

            // Act
            var loadResult = await subject.LoadEntityAsync(EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Same(expectedLoadResult, loadResult);
        }

        [Fact]
        public async Task LoadEntityAsyncReflectsStoredEntityTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var entity = await StoreEntityAsync();
            var subject = Fixture.Create<IEntityStorage>();
            var queryProcessor = new TestDomainQueryProcessor();

            // Act
            var loadResult = (IFoundEntityQueryResult)await subject.LoadEntityAsync(EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            // Assert
            Assert.Same(entity, loadResult.Entity);
        }

        [Fact]
        public async Task LoadEntityAsyncReflectsDeletedEntityTest()
        {
            // Arrange
            EngineReturnsOneResult();

            await DeleteEntityAsync();

            var subject = Fixture.Create<IEntityStorage>();
            var queryProcessor = new TestDomainQueryProcessor();

            // Act
            var loadResult = await subject.LoadEntityAsync(
                EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            // Assert
            Assert.False(loadResult.IsFound());
        }

        [Fact]
        public async Task LoadEntityAsyncSetsEntityMetadataTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var subject = Fixture.Create<IEntityStorage>();
            var queryProcessor = new TestDomainQueryProcessor();

            // Act
            var loadResult = (IFoundEntityQueryResult)await subject.LoadEntityAsync(
                EntityIdentifier, queryProcessor, Fixture.Create<CancellationToken>());

            // Assert
            var entityDescriptor = new EntityDescriptor(EntityIdentifier.EntityType, loadResult.Entity);
            Assert.Equal(loadResult.EntityIdentifier.EntityId, subject.MetadataManager.GetId(entityDescriptor));
            Assert.Equal(loadResult.ConcurrencyToken, subject.MetadataManager.GetConcurrencyToken(entityDescriptor));
            Assert.Equal(loadResult.Revision, subject.MetadataManager.GetRevision(entityDescriptor));
            Assert.Empty(subject.MetadataManager.GetUncommittedEvents(entityDescriptor));
        }

        [Fact]
        public async Task LoadedEntitiesReflectCreatedEntityTest()
        {
            // Arrange
            EngineReturnsNoResult();

            var entity = await StoreEntityAsync();
            var subject = Fixture.Create<IEntityStorage>();

            // Act 
            var loadedEntities = subject.LoadedEntities;

            // Assert
            Assert.Collection(loadedEntities, loadResult => Assert.Same(entity, loadResult.Entity));
        }

        [Fact]
        public async Task LoadedEntitiesReflectDeletedEntityTest()
        {
            // Arrange
            EngineReturnsOneResult();

            await DeleteEntityAsync();
            var subject = Fixture.Create<IEntityStorage>();

            // Act 
            var loadedEntities = subject.LoadedEntities;

            // Assert
            Assert.Empty(loadedEntities);
        }

        [Fact]
        public async Task LoadedEntitiesReflectUpdatedEntityTest()
        {
            // Arrange
            EngineReturnsOneResult();

            var entity = await StoreEntityAsync();
            var subject = Fixture.Create<IEntityStorage>();

            // Act 
            var loadedEntities = subject.LoadedEntities;

            // Assert
            Assert.Collection(loadedEntities, loadResult => Assert.Same(entity, loadResult.Entity));
        }

        [Fact]
        public async Task CommitAsyncCallsEngineCorrectlyTest()
        {
            // Arrange
            EngineReturnsOneResult();
            object commitAttempt = null;
            ObserveCommitAttempt(observedCommitAttempt => commitAttempt = observedCommitAttempt);

            var entity = await StoreEntityAsync();
            var subject = Fixture.Create<IEntityStorage>();

            // Act 
            var commitResult = await subject.CommitAsync(Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.ConcurrencyFailure, commitResult);

            var entriesProperty = commitAttempt.GetType().GetProperty("Entries", BindingFlags.Public | BindingFlags.Instance);

            var entries = ((IEnumerable)entriesProperty.GetValue(commitAttempt)).Cast<ICommitAttemptEntry>();
            Assert.Collection(entries, entry =>
            {
                Assert.Equal(EntityIdentifier, entry.EntityIdentifier);
                Assert.Equal(CommitOperation.Store, entry.Operation);
                Assert.Equal(FoundResult.Revision + 1, entry.Revision);
                Assert.NotEqual(FoundResult.ConcurrencyToken, entry.ConcurrencyToken);
                Assert.Equal(FoundResult.Revision, entry.ExpectedRevision);
                //Assert.Equal(entity, entry.Entity); // TODO: This is copied
                Assert.Equal(DomainEvents, entry.DomainEvents);
            });
        }

        [Fact]
        public async Task CommitAsyncRollsBackStorageTest()
        {
            // Arrange
            EngineReturnsNoResult();

            await StoreEntityAsync();

            var subject = Fixture.Create<IEntityStorage>();

            // Act 
            await subject.CommitAsync(Fixture.Create<CancellationToken>());

            // Assert
            Assert.Empty(subject.LoadedEntities);
        }

        [Fact]
        public async Task RollbackAsyncRollsBackStorageTest()
        {
            // Arrange
            EngineReturnsNoResult();

            await StoreEntityAsync();

            var subject = Fixture.Create<IEntityStorage>();

            // Act 
            await subject.RollbackAsync(Fixture.Create<CancellationToken>());

            // Assert
            Assert.Empty(subject.LoadedEntities);
        }

        [Fact]
        public async Task CommitAsyncWithNoRecordedOperationsIsSuccessTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorage>();

            // Act 
            var commitResult = await subject.CommitAsync(Fixture.Create<CancellationToken>());

            // Assert
            Assert.Equal(EntityCommitResult.Success, commitResult);
        }

        [Fact]
        public async Task CommitAsyncWithNoRecordedOperationsDoesNotCallEngineTest()
        {
            // Arrange
            var subject = Fixture.Create<IEntityStorage>();

            // Act 
            await subject.CommitAsync(Fixture.Create<CancellationToken>());

            // Assert
            Fixture.Create<Mock<IEntityStorageEngine>>()
               .Verify(storageEngine => storageEngine.ProcessCommitAttemptAsync(
                   It.IsAny<CommitAttempt<CommitAttemptEntryTypeMatcher>>(), Fixture.Create<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StoreAsyncClearsEventsTest()
        {
            // Arrange
            EngineReturnsOneResult();
            var entityDescriptor = new EntityDescriptor(_domainEntityBaseType, FoundResult.Entity);

            // Act
            await StoreEntityAsync((DomainEntity1)FoundResult.Entity);

            // Assert
            Assert.Equal(DomainEventCollection.Empty, Fixture.Create<IEntityMetadataManager>().GetUncommittedEvents(entityDescriptor));
        }

        [Fact]
        public async Task DeleteAsyncClearsEventsTest()
        {
            // Arrange
            EngineReturnsOneResult();
            var entityDescriptor = new EntityDescriptor(_domainEntityBaseType, FoundResult.Entity);

            // Act
            await DeleteEntityAsync((DomainEntity1)FoundResult.Entity);

            // Assert
            Assert.Equal(DomainEventCollection.Empty, Fixture.Create<IEntityMetadataManager>().GetUncommittedEvents(entityDescriptor));
        }
    }
}
