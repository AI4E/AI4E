namespace AI4E.Storage.Domain
{
    public interface IEntityIdAccessor<TId, TEntityBase>
        where TEntityBase : class
    {
        TId GetId(TEntityBase entity);
    }
}
