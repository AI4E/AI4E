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
using System.Collections.Generic;
using System.Linq;
using AI4E.Serialization;
using AI4E.Storage.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage
{
    public static class StorageBuilderExtension
    {
        public static IStorageBuilder UseDatabase<TDatabase>(this IStorageBuilder builder)
            where TDatabase : class, IDatabase
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var services = builder.Services;

            services.AddSingleton<IDatabase, TDatabase>();
            services.AddSingleton(p => p.GetRequiredService<IDatabase>() as IFilterableDatabase);
            services.AddSingleton(p => p.GetRequiredService<IDatabase>() as IQueryableDatabase);

            services.UseTransactionSubsystem();

            return builder;
        }

        public static IStorageBuilder UseDatabase<TDatabase>(this IStorageBuilder builder, Func<IServiceProvider, TDatabase> factory)
            where TDatabase : class, IDatabase
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var services = builder.Services;

            services.AddSingleton<IDatabase, TDatabase>(factory);
            services.AddSingleton(p => p.GetRequiredService<IDatabase>() as IFilterableDatabase);
            services.AddSingleton(p => p.GetRequiredService<IDatabase>() as IQueryableDatabase);

            services.UseTransactionSubsystem();

            return builder;
        }

        private static void UseTransactionSubsystem(this IServiceCollection services)
        {
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<IEntryStateTransformerFactory, EntryStateTransformerFactory>();
            services.AddSingleton<IEntryStorageFactory, EntryStorageFactory>();
            services.AddSingleton<ITransactionStateTransformer, TransactionStateTransformer>();
            services.AddSingleton<ITransactionStateStorage, TransactionStateStorage>();
            services.AddSingleton<ITransactionManager, TransactionManager>();
            services.AddTransient(provider => provider.GetRequiredService<ITransactionManager>().CreateStore());
            services.AddTransient(provider => provider.GetRequiredService<ITransactionManager>().CreateStore() as IQueryableTransactionalDatabase);
        }

        public static IStorageBuilder Configure(this IStorageBuilder builder, Action<StorageOptions> configuration)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            builder.Services.Configure(configuration);

            return builder;
        }

        public static IStorageBuilder ExtendStorage<TBucketId, TStreamId>(this IStorageBuilder builder, IEnumerable<IStorageExtension<TBucketId, TStreamId>> hooks)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (hooks == null)
                throw new ArgumentNullException(nameof(hooks));


            // TODO
            return builder; // .Configure(options => options.Extensions.AddRange(hooks));
        }

        public static IStorageBuilder ExtendStorage<TBucketId, TStreamId>(this IStorageBuilder builder, params IStorageExtension<TBucketId, TStreamId>[] hooks)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (hooks == null)
                throw new ArgumentNullException(nameof(hooks));

            return ExtendStorage(builder, hooks);
        }

        public static IStorageBuilder WithSerialization(this IStorageBuilder builder, ISerializer serializer)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            builder.Services.AddSingleton(serializer);

            return builder;
        }

        public static IStorageBuilder WithBinarySerialization(this IStorageBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.AddSingleton<ISerializer, BinarySerializer>();

            return builder;
        }

        public static IStorageBuilder WithJsonSerialization(this IStorageBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.AddSingleton<ISerializer, JsonSerializer>();

            return builder;
        }

        public static IStorageBuilder WithBsonSerialization(this IStorageBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.AddSingleton<ISerializer, BsonSerializer>();

            return builder;
        }

        public static IStorageBuilder WithEncryption(this IStorageBuilder builder, byte[] encryptionKey)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var wrapped = builder.Services.GetService<ISerializer>();

            if (wrapped == null)
            {
                throw new InvalidOperationException("A serializer must be specified in order to use encryption.");
            }

            builder.Services.AddSingleton<ISerializer>(new RijndaelSerializer(wrapped, encryptionKey));

            return builder;
        }

        public static IStorageBuilder WithCompression(this IStorageBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var wrapped = builder.Services.GetService<ISerializer>();

            if (wrapped == null)
            {
                throw new InvalidOperationException("A serializer must be specified in order to use compression.");
            }

            builder.Services.AddSingleton(new GZipSerializer(wrapped));

            return builder;
        }

        // TODO: This is a duplicate
        private static T GetService<T>(this IServiceCollection services)
        {
            return (T)services
                .LastOrDefault(d => d.ServiceType == typeof(T))
                ?.ImplementationInstance;
        }
    }
}
