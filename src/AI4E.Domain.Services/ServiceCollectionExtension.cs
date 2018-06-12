/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using AI4E.Storage;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace AI4E.Domain.Services
{
    public static class ServiceCollectionExtension
    {
        public static void AddDomainServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddTransient<IReferenceResolver, ReferenceResolver>();
            services.AddTransient<ISerializerSettingsResolver<Guid, DomainEvent, AggregateRoot>, SerializerSettingsResolver>();
        }

        public static IStorageBuilder AddStorage(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return services.AddStorage<Guid, DomainEvent, AggregateRoot>();
        }

        public static IStorageBuilder AddStorage(this IServiceCollection services, Action<StorageOptions> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return services.AddStorage<Guid, DomainEvent, AggregateRoot>(configuration);
        }
    }

    public sealed class SerializerSettingsResolver : ISerializerSettingsResolver<Guid, DomainEvent, AggregateRoot>
    {
        public JsonSerializerSettings ResolveSettings(IEntityStore<Guid, DomainEvent, AggregateRoot> entityStore)
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            };

            settings.Converters.Add(new ReferenceConverter(new ReferenceResolver(entityStore)));

            return settings;
        }
    }
}
