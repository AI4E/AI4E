using AI4E;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Products.Api
{
    public class ProductRenameCommand : ConcurrencySafeCommand
    {
        public ProductRenameCommand(Guid id, Guid concurrencyToken, string name) : base(id, concurrencyToken)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
