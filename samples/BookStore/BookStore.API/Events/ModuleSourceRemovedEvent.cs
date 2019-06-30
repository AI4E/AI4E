using System;
using AI4E;

namespace BookStore.Events
{
    public sealed class ModuleSourceRemovedEvent : Event
    {
        public ModuleSourceRemovedEvent(Guid id) : base(id) { }
    }
}
