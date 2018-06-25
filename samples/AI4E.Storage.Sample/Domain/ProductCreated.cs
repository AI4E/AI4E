using System;
using AI4E.Domain;

namespace AI4E.Storage.Sample.Domain
{
    public sealed class ProductCreated : DomainEvent
    {
        public ProductCreated(Guid aggregateId, ProductName productName) : base(aggregateId)
        {
            ProductName = productName;
        }

        public ProductName ProductName { get; }
    }
}
