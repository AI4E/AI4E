using System;

namespace AI4E.Storage.Sample.Api
{
    public sealed class ProductCreateCommand : Command
    {
        public ProductCreateCommand(Guid id, string productName) : base(id)
        {
            ProductName = productName;
        }

        public string ProductName { get; }
    }
}
