using System;
using System.Threading;
using AI4E.Internal;

namespace AI4E.Storage.Domain
{
    public sealed class DefaultEntityIdAccessor<TId, TEntityBase> : IEntityIdAccessor<TId, TEntityBase>
        where TEntityBase : class
    {
        private static Lazy<Func<TEntityBase, TId>> _idAccessor;

        static DefaultEntityIdAccessor()
        {
            _idAccessor = new Lazy<Func<TEntityBase, TId>>(BuildIdAccessor, LazyThreadSafetyMode.PublicationOnly);
        }

        private Func<TEntityBase, TId> IdAccessor => _idAccessor.Value;

        private static Func<TEntityBase, TId> BuildIdAccessor()
        {
            var idType = DataPropertyHelper.GetIdType<TEntityBase>();

            if (idType == null)
            {
                throw new InvalidOperationException($"Unable to access the id on type '{ typeof(TEntityBase).FullName }'.");
            }

            if (!typeof(TId).IsAssignableFrom(idType))
            {
                throw new InvalidOperationException($"The id of type '{ typeof(TEntityBase).FullName }' is incompatible with the expected id type '{typeof(TId)}'.");
            }

            return DataPropertyHelper.GetIdAccessor<TId, TEntityBase>();
        }

        public TId GetId(TEntityBase entity)
        {
            return IdAccessor(entity);
        }
    }
}
