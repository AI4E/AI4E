using System;

namespace AI4E.Storage.InMemory
{
    public static class StorageBuilderExtension
    {
        public static IStorageBuilder UseInMemoryDatabase(this IStorageBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.UseDatabase<InMemoryDatabase>();

            return builder;
        }
    }
}
