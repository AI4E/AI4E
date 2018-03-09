using System;

namespace AI4E.Storage
{
    public abstract class ProcessManager<TEntity> : MessageHandler
    {
        [ProcessManagerEntityDeleteFlag]
        public bool IsMarkedAsDeleted { get; internal set; }

        [ProcessManagerEntity]
        public TEntity Entity { get; set; }

        [NoAction]
        protected void MarkAsDeleted()
        {
            IsMarkedAsDeleted = true;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ProcessManagerEntityAttribute : Attribute
    {
        public Type EntityType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ProcessManagerEntityDeleteFlag : Attribute { }
}
