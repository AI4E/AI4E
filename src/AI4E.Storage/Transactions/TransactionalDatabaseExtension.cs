using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Transactions
{
    public static class TransactionalDatabaseExtension
    {
        public static async Task UnconditionalWriteAsync(this ITransactionalDatabase database,
                                                         Func<IScopedTransactionalDatabase, CancellationToken, Task> operation,
                                                         CancellationToken cancellation = default)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            IScopedTransactionalDatabase transactionalDatabase = null;
            try
            {
                do
                {
                    transactionalDatabase?.Dispose();

                    transactionalDatabase = database.CreateScope();
                    try
                    {
                        await operation(transactionalDatabase, cancellation);
                    }
                    catch (TransactionAbortedException)
                    {
                        continue;
                    }
                }
                while (!await transactionalDatabase.TryCommitAsync(cancellation));
            }
            finally
            {
                transactionalDatabase?.Dispose();
            }
        }

        public static IAsyncEnumerable<TData> UnconditionalReadAsync<TData>(this ITransactionalDatabase database,
                                                                            Expression<Func<TData, bool>> predicate,
                                                                            CancellationToken cancellation = default)
            where TData : class
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            async Task<IEnumerable<TData>> PerformReadAsync()
            {
                IScopedTransactionalDatabase transactionalDatabase = null;
                IEnumerable<TData> result = null;

                try
                {
                    do
                    {
                        transactionalDatabase?.Dispose();

                        transactionalDatabase = database.CreateScope();
                        try
                        {
                            // We have to load all data to memory. 
                            // Otherwise the data would be queried lazily by the caller after the transaction ended.
                            result = await transactionalDatabase.GetAsync(predicate, cancellation);
                        }
                        catch (TransactionAbortedException)
                        {
                            continue;
                        }
                    }
                    while (!await transactionalDatabase.TryCommitAsync(cancellation));
                }
                finally
                {
                    transactionalDatabase?.Dispose();
                }

                Assert(result != null);

                return result;
            }

            return PerformReadAsync().ToAsyncEnumerable();
        }
    }
}
