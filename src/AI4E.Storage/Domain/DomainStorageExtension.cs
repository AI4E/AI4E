using System;
using System.Collections.Generic;

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
    }
}
