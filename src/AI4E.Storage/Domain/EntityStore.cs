using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

namespace AI4E.Storage.Domain
{
    public sealed partial class EntityStore<TId, TEventBase, TEntityBase> : IEntityStore<TId, TEventBase, TEntityBase>
        where TEventBase : class
        where TEntityBase : class
    {
        private static readonly Lazy<Func<TId, TId, bool>> _idEqualityComparer;

        #region Fields

        private readonly IEntityStorageEngine _storageEngine;
        private readonly IEntityIdAccessor<TId, TEntityBase> _entityIdAccessor;

        #endregion

        #region C'tor

        static EntityStore()
        {
            Func<TId, TId, bool> IdEqualityComparerFactory()
            {
                return DataPropertyHelper.BuildIdEquality<TId>().Compile();
            }

            _idEqualityComparer = new Lazy<Func<TId, TId, bool>>(IdEqualityComparerFactory, LazyThreadSafetyMode.PublicationOnly);
        }

        public EntityStore(IEntityStorageEngine storageEngine,
                           IEntityIdAccessor<TId, TEntityBase> entityIdAccessor)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (entityIdAccessor == null)
                throw new ArgumentNullException(nameof(entityIdAccessor));

            _storageEngine = storageEngine;
            _entityIdAccessor = entityIdAccessor;
        }

        #endregion

        private Func<TId, TId, bool> IdEqualityComparer => _idEqualityComparer.Value;

        public ValueTask<TEntityBase> GetByIdAsync(Type entityType, TId id, CancellationToken cancellation = default)
        {
            return GetByIdAsync(entityType, id, revision: default, cancellation);
        }

        public async ValueTask<TEntityBase> GetByIdAsync(Type entityType, TId id, long revision, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            if (!typeof(TEntityBase).IsAssignableFrom(entityType))
                throw new ArgumentException($"The argument must specify a type assignable to '{ typeof(TEntityBase).FullName }'.", nameof(entityType));

            if (IdEqualityComparer(id, default))
                return null;

            Assert(id != null);

            var stringifiedId = id.ToString();

            Assert(stringifiedId != null); // TODO: Does this have to be a runtime check?

            var result = await _storageEngine.GetByIdAsync(entityType, stringifiedId, revision, cancellation);

            Assert(result != null, result is TEntityBase);
            Assert(result != null, entityType.IsAssignableFrom(result.GetType()));

            return result as TEntityBase;
        }

        public IAsyncEnumerable<TEntityBase> GetAllAsync(Type entityType, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (!typeof(TEntityBase).IsAssignableFrom(entityType))
                throw new ArgumentException($"The argument must specify a type assignable to '{ typeof(TEntityBase).FullName }'.", nameof(entityType));

            return _storageEngine.GetAllAsync(entityType, cancellation)
                                 .AssertEach(p => p is TEntityBase)
                                 .Cast<TEntityBase>();
        }

        public IAsyncEnumerable<TEntityBase> GetAllAsync(CancellationToken cancellation = default)
        {
            return _storageEngine.GetAllAsync(cancellation)
                                 .AssertEach(p => p is TEntityBase)
                                 .Cast<TEntityBase>();
        }

        public Task StoreAsync(Type entityType, TEntityBase entity, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!typeof(TEntityBase).IsAssignableFrom(entityType))
                throw new ArgumentException($"The argument must specify a type assignable to '{ typeof(TEntityBase).FullName }'.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The argument must be of type '{ entityType.FullName }' or a derived type.", nameof(entity));

            var id = _entityIdAccessor.GetId(entity);

            Assert(id != null); // TODO: Does this have to be a runtime check?

            var stringifiedId = id.ToString();

            Assert(stringifiedId != null); // TODO: Does this have to be a runtime check?

            return _storageEngine.StoreAsync(entityType, entity, stringifiedId, cancellation);
        }

        public Task DeleteAsync(Type entityType, TEntityBase entity, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!typeof(TEntityBase).IsAssignableFrom(entityType))
                throw new ArgumentException($"The argument must specify a type assignable to '{ typeof(TEntityBase).FullName }'.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The argument must be of type '{ entityType.FullName }' or a derived type.", nameof(entity));

            var id = _entityIdAccessor.GetId(entity);

            Assert(id != null); // TODO: Does this have to be a runtime check?

            var stringifiedId = id.ToString();

            Assert(stringifiedId != null); // TODO: Does this have to be a runtime check?

            return _storageEngine.DeleteAsync(entityType, entity, stringifiedId, cancellation);
        }

        #region Disposal

        public void Dispose()
        {
            _storageEngine.Dispose();
        }

        #endregion
    }
}
