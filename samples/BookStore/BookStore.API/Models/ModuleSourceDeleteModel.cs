using System;

namespace BookStore.Models
{
    public sealed class ModuleSourceDeleteModel
    {
        public Guid Id { get; set; }
        public string ConcurrencyToken { get; set; }
        public string Name { get; set; }
    }
}
