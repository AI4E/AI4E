using System;
using AI4E;
using AI4E.Modularity.Host;
using AI4E.Validation;
using BookStore.Commands;

namespace BookStore.Server.Services
{
    public sealed class ModuleSourceCommandHandler : MessageHandler<IModuleSource>
    {
        [Validate]
        [CreatesEntity(AllowExisingEntity = false)]
        public void Handle(ModuleSourceCreateCommand command)
        {
            var moduleSourceName = new ModuleSourceName(command.Name);
            var moduleSourceLocation = new FileSystemModuleSourceLocation(command.Location);

            Entity = new FileSystemModuleSource(command.Id, moduleSourceName, moduleSourceLocation);
        }

        [Validate]
        public void Handle(ModuleSourceRenameCommand command)
        {
            Entity.Name = (ModuleSourceName)command.Name;
        }

        public void Handle(ModuleSourceDeleteCommand command)
        {
            (Entity as IDisposable)?.Dispose();
            MarkAsDeleted();
        }

        public void Validate(ModuleSourceCreateCommand command, ValidationResultsBuilder validationResultsBuilder)
        {
            validationResultsBuilder.Validate(ModuleSourceName.IsValid, command.Name, nameof(command.Name));
            validationResultsBuilder.Validate(FileSystemModuleSourceLocation.IsValid, command.Location, nameof(command.Location));
        }

        public void Validate(ModuleSourceRenameCommand command, ValidationResultsBuilder validationResultsBuilder)
        {
            validationResultsBuilder.Validate(ModuleSourceName.IsValid, command.Name, nameof(command.Name));
        }
    }
}
