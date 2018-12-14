using System.Collections.Generic;
using System.Linq;
using AI4E.Modularity.Host;
using AI4E.Modularity.Hosting.Sample.Api;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    public sealed class ModuleSourceCommandHandler : MessageHandler<IModuleSource>
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

            if (Entity is FileSystemModuleSource fileSystemModuleSource)
            {
                fileSystemModuleSource.Location = (FileSystemModuleSourceLocation)command.Location;
                return Success();
            }

            return Failure();
        }

        public IDispatchResult Handle(ModuleSourceRenameCommand command)
        {
            var validationResults = Validate(command);

            if (validationResults.Any())
            {
                return ValidationFailure(validationResults);
            }

            if (Entity is FileSystemModuleSource fileSystemModuleSource)
            {
                fileSystemModuleSource.Name = (ModuleSourceName)command.Name;
                return Success();
            }

            return Failure();
        }

        public void Handle(ModuleSourceRemoveCommand command)
        {
            (Entity as FileSystemModuleSource)?.Dispose();
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
}
