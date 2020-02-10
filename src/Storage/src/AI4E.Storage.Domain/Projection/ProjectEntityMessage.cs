using System;

namespace AI4E.Storage.Domain
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
