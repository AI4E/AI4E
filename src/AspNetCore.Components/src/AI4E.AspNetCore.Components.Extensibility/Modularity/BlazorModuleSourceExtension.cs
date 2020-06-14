using System;

namespace AI4E.AspNetCore.Components.Modularity
{
    public static class BlazorModuleSourceExtension
    {
        public static IBlazorModuleSource Configure(
            this IBlazorModuleSource moduleSource,
            Func<IBlazorModuleDescriptor, IBlazorModuleDescriptor> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            return new BlazorModuleSourceWrapper(moduleSource, configuration);
        }
    }
}
