using System;
using System.Collections.Generic;
using System.Linq;
using AI4E.Modularity.Hosting.Sample.Api;
using AI4E.Modularity.Hosting.Sample.Domain;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleSourceCommandHandler : MessageHandler<FileSystemModuleSource>
    {
        [CreatesEntity(AllowExisingEntity = false)]
        public IDispatchResult Handle(ModuleSourceAddCommand command)
        {
            var validationResults = Validate(command);

            if (validationResults.Any())
            {
                return ValidationFailure(validationResults);
            }

            var moduleSourceName = new ModuleSourceName(command.Name);
            var moduleSourceLocation = new FileSystemModuleSourceLocation(command.Location);

            Entity = new FileSystemModuleSource(command.Id, moduleSourceName, moduleSourceLocation);

            return Success();
        }

        public IDispatchResult Handle(ModuleSourceUpdateLocationCommand command)
        {
            var validationResults = Validate(command);

            if (validationResults.Any())
            {
                return ValidationFailure(validationResults);
            }

            Entity.Location = (FileSystemModuleSourceLocation)command.Location;
            return Success();
        }

        public IDispatchResult Handle(ModuleSourceRenameCommand command)
        {
            var validationResults = Validate(command);

            if (validationResults.Any())
            {
                return ValidationFailure(validationResults);
            }

            Entity.Name = (ModuleSourceName)command.Name;
            return Success();
        }

        public void Handle(ModuleSourceRemoveCommand command)
        {
            Entity.Dispose();
            MarkAsDeleted();
        }

        private IEnumerable<ValidationResult> Validate(ModuleSourceAddCommand command)
        {
            var validationResultsBuilder = new ValidationResultsBuilder();

            validationResultsBuilder.Validate(ModuleSourceName.IsValid, command.Name, nameof(command.Name));
            validationResultsBuilder.Validate(FileSystemModuleSourceLocation.IsValid, command.Location, nameof(command.Location));

            return validationResultsBuilder.GetValidationResults();
        }

        private IEnumerable<ValidationResult> Validate(ModuleSourceUpdateLocationCommand command)
        {
            var validationResultsBuilder = new ValidationResultsBuilder();

            validationResultsBuilder.Validate(FileSystemModuleSourceLocation.IsValid, command.Location, nameof(command.Location));

            return validationResultsBuilder.GetValidationResults();
        }

        private IEnumerable<ValidationResult> Validate(ModuleSourceRenameCommand command)
        {
            var validationResultsBuilder = new ValidationResultsBuilder();

            validationResultsBuilder.Validate(ModuleSourceName.IsValid, command.Name, nameof(command.Name));

            return validationResultsBuilder.GetValidationResults();
        }
    }

    public static class ValidationResultsBuilderExtension
    {
        public static void Validate<T>(this ValidationResultsBuilder validationResultsBuilder,
                                       ValidationFunction<T> validationFunction,
                                       T value,
                                       string member)
        {
            if (validationResultsBuilder == null)
                throw new ArgumentNullException(nameof(validationResultsBuilder));

            if (validationFunction == null)
                throw new ArgumentNullException(nameof(validationFunction));

            if (!validationFunction(value, out var message))
            {
                validationResultsBuilder.AddValidationResult(member, message);
            }
        }

        public delegate bool ValidationFunction<T>(T value, out string message);
    }
}
