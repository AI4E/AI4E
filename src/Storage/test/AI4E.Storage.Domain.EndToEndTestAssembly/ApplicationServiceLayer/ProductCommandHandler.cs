using System;
using AI4E.Messaging;
using AI4E.Messaging.Validation;
using AI4E.Storage.Domain.EndToEndTestAssembly.API;
using AI4E.Storage.Domain.EndToEndTestAssembly.Models;

namespace AI4E.Storage.Domain.EndToEndTestAssembly.ApplicationServiceLayer
{
    public sealed class ProductCommandHandler : MessageHandler<Product>
    {
        public ProductCommandHandler(IEntityMetadataManager metadataManager)
        {
            if (metadataManager is null)
                throw new ArgumentNullException(nameof(metadataManager));

            MetadataManager = metadataManager;
        }

        public IEntityMetadataManager MetadataManager { get; }

        [Validate, CreatesEntity(AllowExisingEntity = false)]
        public void Handle(ProductCreateCommand command)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            Entity = new Product
            {
                Id = command.Id,
                Name = command.Name,
                Price = command.Price
            };

            // TODO
            MetadataManager.AddEvent(
                new EntityDescriptor(typeof(Product), Entity),
                new DomainEvent(typeof(ProductCreated), new ProductCreated(command.Id, command.Name, command.Price)));
        }

        [Validate]
        public void Handle(ProductUpdateCommand command)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            Entity.Name = command.Name;
            Entity.Price = command.Price;

            // TODO
            MetadataManager.AddEvent(
                new EntityDescriptor(typeof(Product), Entity),
                new DomainEvent(typeof(ProductUpdated), new ProductUpdated(command.Id, command.Name, command.Price)));  // TODO: Simplify this
        }

        public void Handle(ProductDeleteCommand command)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            MarkAsDeleted();

            // TODO
            MetadataManager.AddEvent(
                new EntityDescriptor(typeof(Product), Entity),
                new DomainEvent(typeof(ProductDeleted), new ProductDeleted(command.Id)));
        }

        public void Validate(ProductCreateCommand command, ValidationResultsBuilder validationResultsBuilder)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            if (validationResultsBuilder is null)
                throw new ArgumentNullException(nameof(validationResultsBuilder));

            var name = command.Name;

            if (string.IsNullOrWhiteSpace(name))
            {
                validationResultsBuilder.AddValidationResult(nameof(command.Name), "The value must be present.");
            }

            var price = command.Price;

            if (price < 0)
            {
                validationResultsBuilder.AddValidationResult(nameof(command.Price), "The value must not be negative.");
            }
        }

        public void Validate(ProductUpdateCommand command, ValidationResultsBuilder validationResultsBuilder)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            if (validationResultsBuilder is null)
                throw new ArgumentNullException(nameof(validationResultsBuilder));

            var name = command.Name;

            if (string.IsNullOrWhiteSpace(name))
            {
                validationResultsBuilder.AddValidationResult(nameof(command.Name), "The value must be present.");
            }

            var price = command.Price;

            if (price < 0)
            {
                validationResultsBuilder.AddValidationResult(nameof(command.Price), "The value must not be negative.");
            }
        }
    }
}
