using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Products.Models
{
    public class ProductRenameModel
    {
        public string Name { get; set; }
        public Guid Id { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }
}
