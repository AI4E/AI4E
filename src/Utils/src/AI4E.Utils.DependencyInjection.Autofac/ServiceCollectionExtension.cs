using AI4E.Utils.DependencyInjection;
using AI4E.Utils.DependencyInjection.Autofac;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AI4EUtilsDependencyInjectionAutofacServiceCollectionExtension
    {
        public static IServiceCollection AddAutofacChildContainerBuilder(this IServiceCollection services)
        {
            services.AddSingleton<IChildContainerBuilder, AutofacChildContainerBuilder>();
            return services;
        }
    }
}
