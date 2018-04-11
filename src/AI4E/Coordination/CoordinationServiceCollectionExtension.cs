using System;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Coordination
{
    public sealed class CoordinationServiceCollectionExtension
    {
        public ICoordinationBuilder AddCoordinationService(IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<ICoordinationManager, CoordinationManager>();

            return new CoordinationBuilder(services);
        }

        private sealed class CoordinationBuilder : ICoordinationBuilder
        {
            public CoordinationBuilder(IServiceCollection services)
            {
                if (services == null)
                    throw new ArgumentNullException(nameof(services));

                Services = services;
            }

            public IServiceCollection Services { get; }
        }
    }

    public interface ICoordinationBuilder
    {
        IServiceCollection Services { get; }
    }

    public static class CoordinationBuilderExtension
    {
        public static ICoordinationBuilder UseCoordinationStorage<TCoordinationStorage>(this ICoordinationBuilder builder)
            where TCoordinationStorage : class, ICoordinationStorage, ISessionStorage
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.AddSingleton<TCoordinationStorage>();
            builder.Services.AddSingleton<ICoordinationStorage>(p => p.GetRequiredService<TCoordinationStorage>());
            builder.Services.AddSingleton<ISessionStorage>(p => p.GetRequiredService<TCoordinationStorage>());

            return builder;
        }

        public static ICoordinationBuilder UseInMemoryStorage(this ICoordinationBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.UseCoordinationStorage<InMemoryCoordinationStorage>();

            return builder;
        }
    }
}
