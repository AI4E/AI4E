using System;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore.Components.Modularity
{
    public interface IBlazorModuleSourceFactory
    {
        IBlazorModuleSource CreateModuleSource();
    }

    public sealed class BlazorModuleSourceFactory : IBlazorModuleSourceFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public BlazorModuleSourceFactory(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public IBlazorModuleSource CreateModuleSource()
        {
            return ActivatorUtilities.CreateInstance<BlazorModuleSource>(_serviceProvider);
        }
    }
}
