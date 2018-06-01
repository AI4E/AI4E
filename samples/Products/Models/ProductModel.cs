using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Products.Models
{
    public class ProductModel
    {
        public Guid Id { get; set; }
        public decimal Price { get; set; }
        public string Name { get; set; }
    }
}
