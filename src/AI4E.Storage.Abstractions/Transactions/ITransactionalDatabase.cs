namespace AI4E.Storage.Transactions
{
    public interface ITransactionalDatabase
    {
        IScopedTransactionalDatabase CreateScope();
    }

    public interface IQueryableTransactionalDatabase : ITransactionalDatabase
    {
        new IQueryableScopedTransactionalDatabase CreateScope();
    }
}
