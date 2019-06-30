using System;
using AI4E;

namespace BookStore.Events
{
    public sealed class ModuleSourceAddedEvent : Event
    {
        public ModuleSourceAddedEvent(Guid id, string location) : base(id)
        {
            Location = location;
        }

        public string Location { get; }
    }
}
