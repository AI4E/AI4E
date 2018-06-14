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
    public static class TransactionManagerExtension
    {
        public static async Task UnconditionalWriteAsync(this ITransactionManager transactionManager,
                                                         Func<ITransactionalDatabase, CancellationToken, Task> operation,
                                                         CancellationToken cancellation = default)
        {
            if (transactionManager == null)
                throw new ArgumentNullException(nameof(transactionManager));

            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            ITransactionalDatabase transactionalDatabase = null;
            do
            {
                if (transactionalDatabase is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                transactionalDatabase = transactionManager.CreateStore();
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

            if (transactionalDatabase is IDisposable disposable2)
            {
                disposable2.Dispose();
            }
        }

        public static IAsyncEnumerable<TData> UnconditionalReadAsync<TData>(this ITransactionManager transactionManager,
                                                                            Expression<Func<TData, bool>> predicate,
                                                                            CancellationToken cancellation = default)
            where TData : class
        {
            if (transactionManager == null)
                throw new ArgumentNullException(nameof(transactionManager));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            async Task<IEnumerable<TData>> PerformReadAsync()
            {
                ITransactionalDatabase transactionalDatabase = null;
                IEnumerable<TData> result = null;

                try
                {
                    do
                    {
                        if (transactionalDatabase is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }

                        transactionalDatabase = transactionManager.CreateStore();
                        try
                        {
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
                    if (transactionalDatabase is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                Assert(result != null);

                return result;
            }

            return PerformReadAsync().ToAsyncEnumerable();
        }
    }
}
