namespace AI4E.Storage.Transactions
{
    public enum TransactionStatus
    {
        Initial = 0, // Currently recording ops
        Prepare = 1, // Transaction is in preparation, no ops may be recorded any more.
        Pending = 2, // Commit started and is processed
        Committed = 3, // The transaction committed 
        CleanedUp = 4, // The transaction is committed and all resources all cleaned up - This is a final state.
        AbortRequested = 5, // The transactions rollback is requested
        Aborted = 6 // The transaction rolled back - This is a final state.
    }
}