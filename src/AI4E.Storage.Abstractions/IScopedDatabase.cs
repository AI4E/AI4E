using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    public interface IScopedDatabase : IDisposable
    {
        Task StoreAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class;

        Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class;

        // Returns all entries of type 'TData' where predicate matches.
        IAsyncEnumerable<TData> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
            where TData : class;

        Task<bool> TryCommitAsync(CancellationToken cancellation = default);
        Task RollbackAsync(CancellationToken cancellation = default);
    }

    public interface IQueryableScopedDatabase : IScopedDatabase
    {
        IAsyncEnumerable<TResult> QueryAsync<TData, TResult>(Func<IQueryable<TData>, IQueryable<TResult>> queryShaper,
                                                             CancellationToken cancellation = default)
            where TData : class;
    }
}
