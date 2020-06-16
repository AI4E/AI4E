using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Utils.DependencyInjection.Autofac
{
    public sealed class AutofacChildContainerBuilder : IChildContainerBuilder
    {
        public AutofacChildContainerBuilder(ILifetimeScope lifetimeScope)
        {
            if (lifetimeScope is null)
                throw new ArgumentNullException(nameof(lifetimeScope));

            LifetimeScope = lifetimeScope;
        }

        public ILifetimeScope LifetimeScope { get; }

        public IChildServiceProvider CreateChildContainer(Action<IServiceCollection> serviceConfiguration)
        {
            if (serviceConfiguration is null)
                throw new ArgumentNullException(nameof(serviceConfiguration));

            return new AutofacChildServiceProvider(BuildChildContainer(LifetimeScope, serviceConfiguration));
        }

        private static ILifetimeScope BuildChildContainer(
            ILifetimeScope parentContainer,
            Action<IServiceCollection> serviceConfiguration)
        {
            if (parentContainer is null)
                throw new ArgumentNullException(nameof(parentContainer));

            if (serviceConfiguration is null)
                throw new ArgumentNullException(nameof(serviceConfiguration));

            void ConfigureChildContainerBuilder(ContainerBuilder containerBuilder)
            {
                var serviceCollection = new ServiceCollection();
                serviceConfiguration(serviceCollection);
                containerBuilder.Populate(serviceCollection);
                containerBuilder.RegisterType<AutofacChildServiceProvider>().As<IServiceProvider>().ExternallyOwned();
            }

            return parentContainer.BeginLifetimeScope(ConfigureChildContainerBuilder);
        }
    }
}
