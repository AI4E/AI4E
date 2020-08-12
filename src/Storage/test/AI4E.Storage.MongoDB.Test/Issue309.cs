using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization;
using Xunit;

namespace AI4E.Storage.MongoDB.Test
{
    public sealed class Issue309
    {
        public Issue309()
        {
            // Register the serializer conventions
            try
            {
                new MongoDatabase(null);
            }
            catch { }
        }

        [Fact]
        public void Issue309Test()
        {
            // Arrange
            var json = InputJsonLoader.Instance.LoadJsonInput();

            // Act
            var eventBatch = BsonSerializer.Deserialize<StoredDomainEventBatch>(json);

            // Assert
            Assert.Collection(eventBatch.DomainEvents,
                storedDomainEvent => Assert.IsType<DeletedDomainEvent<Entity>>(storedDomainEvent.Event),
                storedDomainEvent => Assert.IsType<OtherDomainEvent>(storedDomainEvent.Event));
        }

        private sealed class StoredDomainEventBatch
        {
            private static List<StoredDomainEvent> CreateStoredDomainEvents(
                List<(Type eventType, BaseClass @event)> domainEvents)
            {
                var result = new List<StoredDomainEvent>(capacity: domainEvents.Count);

                // Do not use LINQ as this allocates heavily.
                foreach (var (eventType, @event) in domainEvents)
                {
                    result.Add(new StoredDomainEvent(eventType, @event));
                }

                return result;
            }

            private StoredDomainEventBatch()
            {
                Id = string.Empty;
                EntityType = typeof(object);
                EntityId = string.Empty;
            }

            public StoredDomainEventBatch(
                bool entityDeleted,
                Type entityType,
                string entityId,
                long entityRevision,
                int entityEpoch,
                string? scope,
                List<(Type eventType, BaseClass @event)> domainEvents)
            {
                Id = "AI4E.Storage.MongoDB.Test.IssueXX+Entity°f9e8f657-ec60-43ed-b291-248b497849b8°2°1°abc";
                EntityDeleted = entityDeleted;
                EntityType = entityType;
                EntityId = entityId;
                EntityRevision = entityRevision;
                EntityEpoch = entityEpoch;
                Scope = scope;
                DomainEvents = CreateStoredDomainEvents(domainEvents);
            }

            public string Id { get; private set; }

            /// <summary>
            /// Gets a boolean value indicating whether the current batch deleted the entity.
            /// </summary>
            /// <remarks>
            /// As the current instance is only created when there are domain-event present for the batch, and this 
            /// returns true, the entity was not deleted but marked as deleted, so the garbage collection procedure
            /// has to take care of actually deleting the entity entry.
            /// </remarks>
            public bool EntityDeleted { get; private set; }

            public Type EntityType { get; private set; }
            public string EntityId { get; private set; }
            public long EntityRevision { get; private set; }
            public int EntityEpoch { get; private set; }

            public string? Scope { get; private set; }

            public List<StoredDomainEvent> DomainEvents { get; private set; } = new List<StoredDomainEvent>();
        }

        private sealed class StoredDomainEvent
        {
            private static readonly BaseClass _emptyObject = new BaseClass();

            public StoredDomainEvent(Type eventType, BaseClass @event)
            {
                EventType = eventType;
                Event = @event;
            }

            private StoredDomainEvent()
            {
                EventType = typeof(BaseClass);
                Event = _emptyObject;
            }

            public Type EventType { get; private set; }
            public BaseClass Event { get; private set; }
        }

        private class BaseClass { }

        private abstract class DomainEventBase<TEntity> : BaseClass
             where TEntity : class
        {
            public DomainEventBase(TEntity entity)
            {
                if (entity is null)
                    throw new ArgumentNullException(nameof(entity));
                Entity = entity;
            }

            public TEntity Entity { get; }
        }

        private sealed class DeletedDomainEvent<TEntity> : DomainEventBase<TEntity>
            where TEntity : class
        {
            public DeletedDomainEvent(TEntity entity) : base(entity) { }
        }

        private sealed class Entity
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public Guid ParentId { get; set; }
            public string? Icon { get; set; }
        }

        private sealed class OtherDomainEvent : BaseClass { }
    }
}
