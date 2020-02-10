using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Test.Mocks
{
    internal sealed class DatabaseScopeMock : IDatabaseScope
    {
        public bool CanCommit { get; set; } = true;
        public object GetResult { get; set; }

        public bool IsDisposed { get; private set; }
        public object Entry { get; private set; }
        public Type EntryType { get; private set; }
        public bool StoreCalled { get; private set; }
        public bool RemoveCalled { get; private set; }
        public bool RollbackCalled { get; private set; }
        public bool TryCommitCalled { get; private set; }
        public bool GetCalled { get; private set; }
        public LambdaExpression Predicate { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return default;
        }

        public ValueTask StoreAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
                   where TEntry : class
        {
            Entry = entry;
            EntryType = typeof(TEntry);
            StoreCalled = true;
            return default;
        }

        public ValueTask RemoveAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            Entry = entry;
            EntryType = typeof(TEntry);
            RemoveCalled = true;
            return default;
        }

        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            GetCalled = true;
            Predicate = predicate;
            return GetResult as IAsyncEnumerable<TEntry>;
        }

        public ValueTask RollbackAsync(CancellationToken cancellation = default)
        {
            RollbackCalled = true;
            return default;
        }

        public ValueTask<bool> TryCommitAsync(CancellationToken cancellation = default)
        {
            TryCommitCalled = true;
            return new ValueTask<bool>(CanCommit);
        }

        public IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
            Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            throw new NotSupportedException();
        }
    }
}
