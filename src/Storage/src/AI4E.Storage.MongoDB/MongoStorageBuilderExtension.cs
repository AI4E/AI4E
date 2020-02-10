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
using AI4E.Storage.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AI4E.Storage
{
    public static class MongoStorageBuilderExtension
    {
        public static IStorageBuilder UseMongoDB(this IStorageBuilder builder)
        {
#pragma warning disable CA1062
            builder.Services.AddOptions();
#pragma warning restore CA1062
            builder.Services.AddSingleton(BuildMongoClient);
            builder.Services.AddSingleton(BuildMongoDatabase);
            builder.UseDatabase<MongoDatabase>();
            
            return builder;
        }

        public static IStorageBuilder UseMongoDB(this IStorageBuilder builder, Action<MongoOptions> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            builder.UseMongoDB();

#pragma warning disable CA1062
            builder.Services.Configure(configuration);
#pragma warning restore CA1062

            return builder;
        }

        public static IStorageBuilder UseMongoDB(this IStorageBuilder builder, string database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            builder.UseMongoDB();

#pragma warning disable CA1062
            builder.Services.Configure<MongoOptions>(options =>
#pragma warning restore CA1062
            {
                options.Database = database;
            });

            return builder;
        }

        private static IMongoDatabase BuildMongoDatabase(IServiceProvider serviceProvider)
        {
            var options = GetMongoOptions(serviceProvider);

            return serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(options.Database);
        }

        private static IMongoClient BuildMongoClient(IServiceProvider serviceProvider)
        {
            var options = GetMongoOptions(serviceProvider);

            return new MongoClient(options.ConnectionString);
        }

        private static MongoOptions GetMongoOptions(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetService<IOptions<MongoOptions>>();
            var options = optionsAccessor?.Value ?? new MongoOptions();
            return options;
        }
    }
}
