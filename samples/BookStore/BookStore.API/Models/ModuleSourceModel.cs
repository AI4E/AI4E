using System;

namespace BookStore.Models
{
    public sealed class ModuleSourceModel
    {
        public Guid Id { get; set; }
        public string ConcurrencyToken { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
    }
}
