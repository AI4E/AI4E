using System;
using Microsoft.Extensions.Logging;

namespace AI4E.Storage.Transactions
{
    public sealed class TransactionalDatabase : ITransactionalDatabase
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IEntryStateStorageFactory _entryStateStorageFactory;
        private readonly IEntryStateTransformerFactory _entryStateTransformerFactory;
        private readonly ILoggerFactory _loggerFactory;

        public TransactionalDatabase(ITransactionManager transactionManager,
                                     IEntryStateStorageFactory entryStateStorageFactory,
                                     IEntryStateTransformerFactory entryStateTransformerFactory,
                                     ILoggerFactory loggerFactory = null)
        {
            if (transactionManager == null)
                throw new ArgumentNullException(nameof(transactionManager));

            if (entryStateStorageFactory == null)
                throw new ArgumentNullException(nameof(entryStateStorageFactory));

            if (entryStateTransformerFactory == null)
                throw new ArgumentNullException(nameof(entryStateTransformerFactory));

            _transactionManager = transactionManager;
            _entryStateStorageFactory = entryStateStorageFactory;
            _entryStateTransformerFactory = entryStateTransformerFactory;
            _loggerFactory = loggerFactory;
        }

        public IScopedTransactionalDatabase CreateScope()
        {
            var logger = _loggerFactory?.CreateLogger<ScopedTransactionalDatabase>();

            return new ScopedTransactionalDatabase(_transactionManager,
                                                   logger);
        }
    }

    public sealed class QueryableTransactionalDatabase : IQueryableTransactionalDatabase
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IEntryStateStorageFactory _entryStateStorageFactory;
        private readonly IEntryStateTransformerFactory _entryStateTransformerFactory;
        private readonly ILoggerFactory _loggerFactory;

        public QueryableTransactionalDatabase(ITransactionManager transactionManager,
                                     IEntryStateStorageFactory entryStateStorageFactory,
                                     IEntryStateTransformerFactory entryStateTransformerFactory,
                                     ILoggerFactory loggerFactory = null)
        {
            if (transactionManager == null)
                throw new ArgumentNullException(nameof(transactionManager));

            if (entryStateStorageFactory == null)
                throw new ArgumentNullException(nameof(entryStateStorageFactory));

            if (entryStateTransformerFactory == null)
                throw new ArgumentNullException(nameof(entryStateTransformerFactory));

            _transactionManager = transactionManager;
            _entryStateStorageFactory = entryStateStorageFactory;
            _entryStateTransformerFactory = entryStateTransformerFactory;
            _loggerFactory = loggerFactory;
        }

        IScopedTransactionalDatabase ITransactionalDatabase.CreateScope()
        {
            return CreateScope();
        }

        public IQueryableScopedTransactionalDatabase CreateScope()
        {
            var logger = _loggerFactory?.CreateLogger<ScopedTransactionalDatabase>();

            throw new NotImplementedException();
        }
    }
}
