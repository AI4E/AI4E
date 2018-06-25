using System;

namespace AI4E.Storage.Sample.Models
{
    public sealed class ProductDeleteModel
    {
        public Guid Id { get; set; }
        public string ConcurrencyToken { get; set; }
    }
}
