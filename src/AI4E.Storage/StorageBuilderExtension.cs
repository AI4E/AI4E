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
using System.Transactions;
using AI4E.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage
{
    public static class StorageBuilderExtension
    {
        public static IStorageBuilder Configure(this IStorageBuilder builder, Action<StorageOptions> configuration)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            builder.Services.Configure(configuration);

            return builder;
        }

        public static IStorageBuilder ExtendStorage<TBucket, TStreamId>(this IStorageBuilder builder, IEnumerable<IStorageExtension<TBucket, TStreamId>> hooks)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (hooks == null)
                throw new ArgumentNullException(nameof(hooks));


            // TODO
            return builder; // .Configure(options => options.Extensions.AddRange(hooks));
        }

        public static IStorageBuilder ExtendStorage<TBucket, TStreamId>(this IStorageBuilder builder, params IStorageExtension<TBucket, TStreamId>[] hooks)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (hooks == null)
                throw new ArgumentNullException(nameof(hooks));

            return ExtendStorage(builder, hooks);
        }

        public static IStorageBuilder WithPersistence<TBucket, TStreamId>(this IStorageBuilder builder, IStreamPersistence<TBucket, TStreamId> persistence)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            builder.Configure(options =>
            {
                options.TransactionScopeOption = TransactionScopeOption.Suppress;
            });

            builder.Services.AddSingleton(persistence);

            return builder;
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

        //public static IStorageBuilder WithProjection(this IStorageBuilder builder)
        //{

        //}

        private static T GetService<T>(this IServiceCollection services)
        {
            return (T)services
                .LastOrDefault(d => d.ServiceType == typeof(T))
                ?.ImplementationInstance;
        }
    }
}
