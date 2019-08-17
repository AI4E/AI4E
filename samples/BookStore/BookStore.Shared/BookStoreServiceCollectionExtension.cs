using BookStore.Alerts;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class BookStoreServiceCollectionExtension
    {
        public static IServiceCollection AddSharedBookStoreServices(this IServiceCollection services)
        {
            services.AddDateTimeProvider();

            // TODO: When running on Blazor (client-side) this should be registered as singleton,
            //       when running server-side scoped.
            services.AddScoped<IAlertMessageManager, AlertMessageManager>();

            return services;
        }
    }
}
