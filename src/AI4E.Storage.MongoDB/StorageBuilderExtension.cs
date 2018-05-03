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
using AI4E.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AI4E.Storage.MongoDB
{
    public static class StorageBuilderExtension
    {
        public static IStorageBuilder UsingMongoDBPersistence(this IStorageBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.AddOptions();

            builder.Services.AddSingleton<IMongoClient>(provider =>
                new MongoClient((provider.GetService<IOptions<MongoOptions>>()?.Value ?? new MongoOptions()).ConnectionString));

            builder.Services.AddSingleton(provider =>
                provider.GetRequiredService<IMongoClient>().GetDatabase((provider.GetService<IOptions<MongoOptions>>()?.Value ?? new MongoOptions()).Database));

            builder.Services.AddSingleton(typeof(IStreamPersistence<,>), typeof(MongoStreamPersistence<,>));
            builder.Services.AddSingleton(typeof(IDataStore), typeof(MongoDataStore));

            builder.Services.AddSingleton<MongoCoordinationStorage>();
            builder.Services.AddSingleton<ICoordinationStorage>(p => p.GetRequiredService<MongoCoordinationStorage>());
            builder.Services.AddSingleton<ISessionStorage>(p => p.GetRequiredService<MongoCoordinationStorage>());
            //builder.Services.AddSingleton(typeof(IRouteMap<>), typeof(MongoRouteMap<>));
            //builder.Services.AddSingleton(typeof(IRouteStore), typeof(MongoRouteStore));
            builder.Services.AddSingleton(typeof(IProjectionDependencyStore<,>), typeof(MongoProjectionDependencyStore<,>));

            return builder;
        }

        public static IStorageBuilder UsingMongoDBPersistence(this IStorageBuilder builder, Action<MongoOptions> configuration)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            builder.UsingMongoDBPersistence();

            builder.Services.Configure(configuration);

            return builder;
        }
    }
}
