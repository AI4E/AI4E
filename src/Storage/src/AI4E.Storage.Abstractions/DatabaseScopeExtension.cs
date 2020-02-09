using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    public static partial class DatabaseScopeExtension
    {
        public static IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            this IDatabaseScope databaseScope,
            CancellationToken cancellation = default)
            where TEntry : class
        {
#pragma warning disable CA1062
            return databaseScope.GetAsync<TEntry>(_ => true, cancellation);
#pragma warning restore CA1062
        }

        public static ValueTask<TEntry?> GetOneAsync<TEntry>(
            this IDatabaseScope databaseScope,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
#pragma warning disable CA1062
            return databaseScope.GetAsync(predicate, cancellation).FirstOrDefaultAsync(cancellation)!;
#pragma warning restore CA1062
        }

        public static ValueTask<TEntry?> GetOneAsync<TEntry>(
            this IDatabaseScope databaseScope,
            CancellationToken cancellation = default)
            where TEntry : class
        {
#pragma warning disable CA1062 
            return databaseScope.GetOneAsync<TEntry>( _ => true, cancellation);
#pragma warning restore CA1062
        }
    }
}
