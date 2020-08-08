using System;
using AI4E.Messaging;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.API
{
    public sealed class ProductDeleteCommand : ConcurrencySafeCommand
    {
        public ProductDeleteCommand(Guid id, string concurrencyToken) : base(id, concurrencyToken) { }
    }
}
