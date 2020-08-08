using System;
using AI4E.Messaging;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.API
{
    public sealed class ProductUpdateCommand : ConcurrencySafeCommand
    {
        public ProductUpdateCommand(
            Guid id, string concurrencyToken, string name, decimal price) : base(id, concurrencyToken)
        {
            Name = name;
            Price = price;
        }

        public string Name { get; }
        public decimal Price { get; }
    }
}
