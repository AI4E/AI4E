using System;

namespace ComponentBaseTest.Models
{
    public sealed class Resource
    {
#nullable disable
        public Guid Id { get; set; }
        public Guid ConcurrencyToken { get; set; }

        public string Name { get; set; }
        public int Amount { get; set; }
        public DateTime? DateOfCreation { get; set; }
#nullable enable
    }
}
