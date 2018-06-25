using System;

namespace AI4E.Storage.Sample.Api
{
    public sealed class ProductDeleteCommand : ConcurrencySafeCommand
    {
        public ProductDeleteCommand(Guid id, string concurrencyToken) : base(id, concurrencyToken) { }
    }
}
