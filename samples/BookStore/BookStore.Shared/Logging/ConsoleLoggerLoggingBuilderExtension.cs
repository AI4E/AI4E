using System;
using BookStore.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Logging
{
    public static class ConsoleLoggerLoggingBuilderExtension
    {
        public static ILoggingBuilder AddBrowserConsole(this ILoggingBuilder builder)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ConsoleLoggerProvider>());
            return builder;
        }

        public static ILoggingBuilder AddBrowserConsole(this ILoggingBuilder builder, Action<ConsoleLoggerOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddBrowserConsole();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
