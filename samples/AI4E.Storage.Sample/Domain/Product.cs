using System;
using AI4E.Domain;

namespace AI4E.Storage.Sample.Domain
{
    public sealed class Product : AggregateRoot
    {
        public Product(Guid id, ProductName productName) : base(id)
        {
            if (productName == default)
                throw new ArgumentDefaultException(nameof(productName));

            ProductName = productName;

            Notify(new ProductCreated(id, productName));
        }

        public ProductName ProductName { get; set; }

        public Price Price { get; set; }
    }
}
