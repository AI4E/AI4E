using System;
using System.Collections.Generic;
using AI4E.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    public static class DomainStorageExtension
    {
        public static IStorageBuilder ExtendStorage(this IStorageBuilder builder,
                                                    IEnumerable<IStorageExtension> extensions)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (extensions == null)
                throw new ArgumentNullException(nameof(extensions));


            // TODO
            return builder; // .Configure(options => options.Extensions.AddRange(hooks));
        }

        public static IStorageBuilder ExtendStorage(this IDomainStorageBuilder builder,
                                                    params IStorageExtension[] extensions)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (extensions == null)
                throw new ArgumentNullException(nameof(extensions));

            return ExtendStorage(builder, extensions);
        }

        public static IStorageBuilder WithSerialization(this IDomainStorageBuilder builder, ISerializer serializer)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            builder.Services.AddSingleton(serializer);

            return builder;
        }

        //public static IStorageBuilder WithBinarySerialization(this IStorageBuilder builder)
        //{
        //    if (builder == null)
        //        throw new ArgumentNullException(nameof(builder));

        //    builder.Services.AddSingleton<ISerializer, BinarySerializer>();

        //    return builder;
        //}

        //public static IStorageBuilder WithJsonSerialization(this IStorageBuilder builder)
        //{
        //    if (builder == null)
        //        throw new ArgumentNullException(nameof(builder));

        //    builder.Services.AddSingleton<ISerializer, JsonSerializer>();

        //    return builder;
        //}

        //public static IStorageBuilder WithBsonSerialization(this IStorageBuilder builder)
        //{
        //    if (builder == null)
        //        throw new ArgumentNullException(nameof(builder));

        //    builder.Services.AddSingleton<ISerializer, BsonSerializer>();

        //    return builder;
        //}

        //public static IStorageBuilder WithEncryption(this IStorageBuilder builder, byte[] encryptionKey)
        //{
        //    if (builder == null)
        //        throw new ArgumentNullException(nameof(builder));

        //    var wrapped = builder.Services.GetService<ISerializer>();

        //    if (wrapped == null)
        //    {
        //        throw new InvalidOperationException("A serializer must be specified in order to use encryption.");
        //    }

        //    builder.Services.AddSingleton<ISerializer>(new RijndaelSerializer(wrapped, encryptionKey));

        //    return builder;
        //}

        //public static IStorageBuilder WithCompression(this IStorageBuilder builder)
        //{
        //    if (builder == null)
        //        throw new ArgumentNullException(nameof(builder));

        //    var wrapped = builder.Services.GetService<ISerializer>();

        //    if (wrapped == null)
        //    {
        //        throw new InvalidOperationException("A serializer must be specified in order to use compression.");
        //    }

        //    builder.Services.AddSingleton(new GZipSerializer(wrapped));

        //    return builder;
        //}
    }
}
