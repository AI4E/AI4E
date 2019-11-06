using Microsoft.Extensions.DependencyInjection;

namespace Routing.Modularity.Sample.PluginA
{
    public sealed class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMessaging();
        }
    }
}
