using AI4E;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Products.Api
{
    public class ProductDeleteCommand : ConcurrencySafeCommand
    {
        public ProductDeleteCommand(Guid id, Guid concurrencyToken) : base(id, concurrencyToken)
        {
        }
    }
}
