using System;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.Models
{
    public sealed class ProductCreated
    {
        public ProductCreated(Guid id, string name, decimal price)
        {
            Id = id;
            Name = name;
            Price = price;
        }

        public Guid Id { get; }
        public string Name { get; }
        public decimal Price { get; }
    }
}
