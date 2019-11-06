using System;
using AI4E.Utils;

namespace Microsoft.Extensions.Hosting
{
    public static class AI4EAspNetCoreHostBuilderExtensions
    {
        /// <summary>
        /// Specify the <see cref="ContextServiceProvider"/> as the used <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">The delegate that configures the <see cref="IServiceProvider"/>.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder UseContextServiceProvider(
            this IHostBuilder hostBuilder, Action<ContextServiceProviderOptions> configure)
        {
            return hostBuilder.UseContextServiceProvider((context, options) => configure(options));
        }

        /// <summary>
        /// Specify the <see cref="ContextServiceProvider"/> as the used <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">The delegate that configures the <see cref="IServiceProvider"/>.</param>
        /// <returns>The <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder UseContextServiceProvider(
            this IHostBuilder hostBuilder,
            Action<HostBuilderContext, ContextServiceProviderOptions> configure)
        {
#pragma warning disable CA1062
            return hostBuilder.UseServiceProviderFactory(context =>
#pragma warning restore CA1062
            {
                var options = new ContextServiceProviderOptions();
                configure(context, options);
                return new ContextServiceProviderFactory(options);
            });
        }
    }
}
