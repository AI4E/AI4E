using AI4E.Modularity.Hosting.Sample.Api;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    using Module = Host.Module;

    public sealed class ModuleCommandHandler : MessageHandler<Module>
    {
        public IDispatchResult Handle(ModuleInstallCommand command)
        {
            var release = Entity.GetRelease(command.Version);

            if (release == null)
                return NotFound();

            release.Install();

            return Success();
        }

        public void Handle(ModuleUninstallCommand command)
        {
            Entity.Uninstall();
        }
    }
}
