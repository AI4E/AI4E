using System;

// TODO: Use EntityIdentifier

namespace AI4E.Storage.Domain.Projection
{
    public sealed class ProjectEntityMessage
    {
        public ProjectEntityMessage(Type entityType, string entityId)
        {
            EntityType = entityType;
            EntityId = entityId;
        }

        public Type EntityType { get; }
        public string EntityId { get; }
    }
}
