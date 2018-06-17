using System;

namespace AI4E.Storage.Projection
{
    public readonly struct EntityDescriptor : IEquatable<EntityDescriptor>
    {
        public EntityDescriptor(Type entityType, string entityId)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityId.Equals(default))
                throw new ArgumentDefaultException(nameof(entityId));

            EntityType = entityType;
            EntityId = entityId;
        }

        public Type EntityType { get; }
        public string EntityId { get; }

        public override bool Equals(object obj)
        {
            return obj is EntityDescriptor entityDescriptor && Equals(entityDescriptor);
        }

        public bool Equals(EntityDescriptor other)
        {
            return other.EntityType == null && EntityType == null || other.EntityType == EntityType && other.EntityId.Equals(EntityId);
        }

        public override int GetHashCode()
        {
            if (EntityType == null)
                return 0;

            return EntityType.GetHashCode() ^ EntityId.GetHashCode();
        }

        public static bool operator ==(in EntityDescriptor left, in EntityDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in EntityDescriptor left, in EntityDescriptor right)
        {
            return !left.Equals(right);
        }
    }
}
