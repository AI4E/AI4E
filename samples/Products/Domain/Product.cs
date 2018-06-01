using AI4E;
using AI4E.Domain;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Products.Domain
{
    public class Product : AggregateRoot
    {
        public Product(Guid id, string name) : base(id)
        {
            Rename(name);
        }

        public string Name { get; private set; }

        [JsonProperty]
        public decimal Price { get; private set; }

        public void Rename(string newName)
        {
            if (String.IsNullOrWhiteSpace(newName))
                throw new ArgumentNullOrWhiteSpaceException(nameof(newName));
            Name = newName;
        }

        public void SetPrice(decimal newPrice)
        {
            if (newPrice < 0)
                throw new ArgumentOutOfRangeException(nameof(newPrice));
    
            Price = newPrice;
        }
    }
}
