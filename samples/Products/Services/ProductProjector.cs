using AI4E.Storage.Projection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Products.Models;
using Products.Domain;

namespace Products.Services
{
    public class ProductProjector : Projection
    {
        public ProductChangePriceModel ProjectToChangePriceModel(Product product)
        {
            return new ProductChangePriceModel { Id = product.Id, Name = product.Name, Price = product.Price, ConcurrencyToken = product.ConcurrencyToken };
        }

        public ProductModel Project(Product product)
        {
            return new ProductModel { Id = product.Id, Name = product.Name, Price = product.Price };
        }

        public ProductRenameModel ProjectToRenameModel(Product product)
        {
            return new ProductRenameModel { Id = product.Id, Name = product.Name, ConcurrencyToken = product.ConcurrencyToken };
        }
    }
}
