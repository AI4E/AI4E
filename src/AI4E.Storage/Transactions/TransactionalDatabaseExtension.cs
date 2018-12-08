using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.AsyncEnumerable;

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

            using (var transactionalDatabase = database.CreateScope())
            {
                do
                {
                    try
                    {
                        await operation(transactionalDatabase, cancellation);

                        if (await transactionalDatabase.TryCommitAsync(cancellation))
                        {
                            break;
                        }
                    }
                    catch (TransactionAbortedException) { }
                    catch (Exception)
                    {
                        // TODO: Log
                        throw;
                    }
                }
                while (true);
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
                IEnumerable<TData> result;

                using (var transactionalDatabase = database.CreateScope())
                {
                    do
                    {
                        try
                        {
                            // We have to load all data to memory. 
                            // Otherwise the data would be queried lazily by the caller after the transaction ended.
                            result = await transactionalDatabase.GetAsync(predicate, cancellation);

                            if (await transactionalDatabase.TryCommitAsync(cancellation))
                            {
                                break;
                            }
                        }
                        catch (TransactionAbortedException) { }
                        catch (Exception)
                        {
                            // TODO: Log
                            throw;
                        }
                    }
                    while (true);
                }

                return result;
            }

            return PerformReadAsync().ToAsyncEnumerable();
        }
    }
}
