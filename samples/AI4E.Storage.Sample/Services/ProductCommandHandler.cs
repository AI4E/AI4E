using AI4E.Storage.Sample.Api;
using AI4E.Storage.Sample.Domain;

namespace AI4E.Storage.Sample.Services
{
    public sealed class ProductCommandHandler : MessageHandler<Product>
    {
        [CreatesEntity(AllowExisingEntity = false)]
        public void Handle(ProductCreateCommand command)
        {
            Entity = new Product(command.Id, new ProductName(command.ProductName));
        }

        public void Handle(ProductRenameCommand command)
        {
            Entity.ProductName = new ProductName(command.ProductName);
        }

        public void Handle(ProductDeleteCommand command)
        {
            MarkAsDeleted();
        }
    }
}
