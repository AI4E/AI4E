using System;
using AI4E.Messaging;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.API
{
    public sealed class ProductCreateCommand : Command
    {
        public ProductCreateCommand(Guid id, string name, decimal price) : base(id)
        {
            Name = name;
            Price = price;
        }

        public string Name { get; }
        public decimal Price { get; }
    }
}
