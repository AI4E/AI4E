/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AI4E.Messaging;
using AI4E.Storage.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Storage
{
    /// <summary>
    /// Contains domain-storage extensions for the <see cref="IStorageBuilder"/> type.
    /// </summary>
    public static class DomainStorageStorageBuilderExtension
    {
        private static IDomainStorageBuilder InternalUseDomainStorage(this IStorageBuilder builder)
        {
            if (!TryGetDomainStorageBuilder(builder, out var domainStorageBuilder))
            {
                var services = builder.Services;

                services.TryAddSingleton<IMessageAccessor, ConventionBasedMessageAccessor>();
                services.TryAddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
                services.TryAddSingleton<IEntityStorageEngine, EntityStorageEngine>();
                services.TryAddScoped<IEntityMetadataManager, EntityMetadataManager>();
                services.TryAddScoped<IEntityStorage, EntityStorage>();
                services.TryAddSingleton<IEntityStorageFactory, EntityStorageFactory>();

                services.TryAddSingleton<IEntityIdFactory, EntityIdFactory>();
                services.TryAddSingleton<IConcurrencyTokenFactory, ConcurrencyTokenFactory>();
                services.TryAddSingleton<ICommitAttemptProcessorRegistry, CommitAttemptProcessorRegistry>();

                AddMessageProcessors(services);

                domainStorageBuilder = new DomainStorageBuilder(builder);
                builder.Services.AddSingleton(domainStorageBuilder);
            }

            return domainStorageBuilder;
        }

        private static bool TryGetDomainStorageBuilder(
            this IStorageBuilder builder,
            [NotNullWhen(true)] out DomainStorageBuilder? domainStorageBuilder)
        {
            domainStorageBuilder = builder.Services.LastOrDefault(
                p => p.ServiceType == typeof(DomainStorageBuilder))?.ImplementationInstance as DomainStorageBuilder;

            return domainStorageBuilder != null;
        }

        /// <summary>
        /// Adds the required domain-storage services to the specified storage-builder.
        /// </summary>
        /// <param name="builder">The storage-builder.</param>
        /// <returns>The storage-builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> is <c>null</c>.</exception>
        public static IStorageBuilder AddDomainStorage(this IStorageBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            _ = InternalUseDomainStorage(builder);
            return builder;
        }

        /// <summary>
        /// Adds the required domain-storage services to the specified storage-builder.
        /// </summary>
        /// <param name="builder">The storage-builder.</param>
        /// <param name="configuration">A delegate that configures the domain-storage.</param>
        /// <returns>The storage-builder.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="builder"/> or <paramref name="configuration"/> is <c>null</c>.
        /// </exception>
        public static IStorageBuilder AddDomainStorage(this IStorageBuilder builder, Action<IDomainStorageBuilder> configuration)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var domainStorageBuilder = InternalUseDomainStorage(builder);
            configuration(domainStorageBuilder);
            return builder;
        }

        private static void AddMessageProcessors(IServiceCollection services)
        {
            services.AddMessaging(options =>
            {
                options.MessageProcessors.Add(MessageProcessorRegistration.Create<EntityMessageHandlerProcessor>());
            });
        }
    }
}
