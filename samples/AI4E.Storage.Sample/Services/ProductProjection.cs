using System.Threading.Tasks;
using AI4E.Storage.Domain;
using AI4E.Storage.Sample.Domain;
using AI4E.Storage.Sample.Models;
using Microsoft.AspNetCore.Mvc;

namespace AI4E.Storage.Sample.Services
{
    public sealed class ProductProjection : Projection.Projection // TODO: Rename Projection namespace
    {
        public ProductListModel ProjectToListModel(Product product)
        {
            if (product == null)
                return null;

            return new ProductListModel
            {
                Id = product.Id,
                Price = product.Price,
                ProductName = product.ProductName
            };
        }

        public ProductDeleteModel ProjectToDeleteModel(Product product,
                                                       [FromServices] IEntityStoragePropertyManager propertyManager)
        {
            if (product == null)
                return null;

            var concurrencyToken = propertyManager.GetConcurrencyToken(product);

            return new ProductDeleteModel
            {
                Id = product.Id,
                ConcurrencyToken = concurrencyToken
            };
        }
    }
}
