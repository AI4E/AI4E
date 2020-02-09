using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Test.Mocks
{
    public sealed class DatabaseMock : IDatabase
    {
        public bool AddCalled { get; private set; }
        public bool ClearCalled { get; private set; }
        public bool GetCalled { get; private set; }
        public bool RemoveCalled { get; private set; }
        public bool UpdateCalled { get; private set; }

        public bool BoolReesult { get; set; }
        public object GetResult { get; set; }

        public LambdaExpression Predicate { get; private set; }
        public Type EntryType { get; private set; }
        public object Entry { get; private set; }

        public bool SupportsScopes => false;

        public ValueTask<bool> AddAsync<TEntry>(TEntry entry, CancellationToken cancellation = default) where TEntry : class
        {
            AddCalled = true;
            EntryType = typeof(TEntry);
            Entry = entry;
            return new ValueTask<bool>(BoolReesult);
        }

        public ValueTask Clear<TEntry>(CancellationToken cancellation = default) where TEntry : class
        {
            ClearCalled = true;
            EntryType = typeof(TEntry);
            return default;
        }

        public IDatabaseScope CreateScope()
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            GetCalled = true;
            EntryType = typeof(TEntry);
            Predicate = predicate;
            return (IAsyncEnumerable<TEntry>)GetResult;
        }

        public IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper, CancellationToken cancellation = default) where TEntry : class
        {
            throw new NotSupportedException();
        }

        public ValueTask<bool> RemoveAsync<TEntry>(TEntry entry, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            RemoveCalled = true;
            EntryType = typeof(TEntry);
            Predicate = predicate;
            Entry = entry;
            return new ValueTask<bool>(BoolReesult);
        }

        public ValueTask<bool> UpdateAsync<TEntry>(TEntry entry, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            UpdateCalled = true;
            EntryType = typeof(TEntry);
            Predicate = predicate;
            Entry = entry;
            return new ValueTask<bool>(BoolReesult);
        }
    }
}
