using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E;

namespace Products.Api
{
    public class ProductChangePriceCommand : ConcurrencySafeCommand
    {
        public ProductChangePriceCommand(Guid id, Guid concurrencyToken, decimal price) : base(id, concurrencyToken)
        {
            Price = price;
        }

        public decimal Price { get; }
    }
}
