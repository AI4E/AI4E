using System;

namespace AI4E.Storage.Sample.Api
{
    public sealed class ProductRenameCommand : ConcurrencySafeCommand
    {
        public ProductRenameCommand(Guid id, string concurrencyToken, string productName) : base(id, concurrencyToken)
        {
            ProductName = productName;
        }

        public string ProductName { get; }
    }
}
