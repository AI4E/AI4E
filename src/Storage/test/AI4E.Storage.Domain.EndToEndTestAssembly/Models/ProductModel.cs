using System;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.Models
{
    public sealed class ProductModel
    {
        public Guid Id { get; set; }

        public string ConcurrencyToken { get; set; }

        public string Name { get; set; }

        public decimal Price { get; set; }
    }
}
