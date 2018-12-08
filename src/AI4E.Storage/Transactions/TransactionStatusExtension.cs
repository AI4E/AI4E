using System;
using AI4E.Utils;

namespace AI4E.Storage.Transactions
{
    public static class TransactionStatusExtension
    {
        public static bool IsCommitted(this TransactionStatus transactionState)
        {
            ValidateState(transactionState);

            return transactionState == TransactionStatus.Committed ||
                   transactionState == TransactionStatus.CleanedUp;
        }

        public static bool IsCommitted(this TransactionStatus? transactionState)
        {
            if (transactionState == null)
                return true;

            ValidateState((TransactionStatus)transactionState);

            return transactionState == TransactionStatus.Committed ||
                   transactionState == TransactionStatus.CleanedUp;
        }

        private static void ValidateState(TransactionStatus transactionState)
        {
            if (!transactionState.IsValid())
                throw new ArgumentException($"The argument must be one of the values defined in '{typeof(TransactionStatus).FullName}'.", nameof(transactionState));
        }
    }
}
