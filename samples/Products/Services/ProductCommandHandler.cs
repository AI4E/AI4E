using AI4E;
using Products.Api;
using Products.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Products.Services
{
    public class ProductCommandHandler : MessageHandler<Product>
    {
        [CreatesEntity(AllowExisingEntity = false)]
        public IDispatchResult Handle(ProductCreateCommand command)
        {
            if(string.IsNullOrWhiteSpace(command.Name))
            {
                return ValidationFailure(nameof(command.Name), "Must not be empty!");
            }

            Entity = new Product(command.Id, command.Name);

            return Success();
        }

        public IDispatchResult Handle(ProductRenameCommand command)
        {
            if(string.IsNullOrWhiteSpace(command.Name))
            {
                return ValidationFailure(nameof(command.Name), "Must not be emtpy!");
            }

            Entity.Rename(command.Name);

            return Success();
        }

        public IDispatchResult Handle(ProductChangePriceCommand command)
        {
            if(command.Price < 0)
            {
                return ValidationFailure(nameof(command.Price), "Must not be negative!");
            }

            Entity.SetPrice(command.Price);

            return Success();
        }

        //public IDispatchResult Handle(ProductDeleteCommand command)
        //{
        //    if(command.Id == null)
        //    {
        //        return ValidationFailure(nameof(command.Id), "Must not be null!");
        //    }

        //    Entity.

        //    return Success();
        //}
    }
}
