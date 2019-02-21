using System;

namespace AI4E
{
    public abstract class Event<TId>
    {
        public Event(TId id)
        {
            Id = id;
        }

        public TId Id { get; }
    }

    public abstract class Event : Event<Guid>
    {
        public Event(Guid id) : base(id) { }
    }
}
