using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Transactions
{
    public static class ScopedTransactionalDatabaseExtension
    {
        private static readonly ConditionalWeakTable<IScopedTransactionalDatabase, TransactionalDataStoreExt> _extensions
          = new ConditionalWeakTable<IScopedTransactionalDatabase, TransactionalDataStoreExt>();

        public static Task StoreAsync(this IScopedTransactionalDatabase database,
                                      object data,
                                      CancellationToken cancellation = default)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data is ValueType)
                throw new ArgumentException("The argument must be a reference type.", nameof(data));

            return GetExtension(database).StoreAsync(data.GetType(), data, cancellation);
        }

        public static Task RemoveAsync(this IScopedTransactionalDatabase database,
                                       object data,
                                       CancellationToken cancellation = default)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data is ValueType)
                throw new ArgumentException("The argument must be a reference type.", nameof(data));

            return GetExtension(database).RemoveAsync(data.GetType(), data, cancellation);
        }

        public static Task StoreAsync(this IScopedTransactionalDatabase database,
                                      Type dataType,
                                      object data,
                                      CancellationToken cancellation = default)
        {
            CheckArguments(database, dataType, data);

            return GetExtension(database).StoreAsync(dataType, data, cancellation);
        }



        public static Task RemoveAsync(this IScopedTransactionalDatabase database,
                                       Type dataType,
                                       object data,
                                       CancellationToken cancellation = default)
        {
            CheckArguments(database, dataType, data);

            return GetExtension(database).RemoveAsync(dataType, data, cancellation);
        }

        private static void CheckArguments(IScopedTransactionalDatabase database, Type dataType, object data)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (dataType.IsValueType)
                throw new ArgumentException("The argument must be a reference type.", nameof(dataType));

            if (!dataType.IsAssignableFrom(data.GetType()))
                throw new ArgumentException($"The specified data must be of type '{dataType.FullName}' or an assignable type.");
        }

        private static TransactionalDataStoreExt GetExtension(IScopedTransactionalDatabase database)
        {
            Assert(database != null);

            var result = _extensions.GetOrCreateValue(database);
            Assert(result != null);

            result.Initialize(database);

            return result;
        }

        private sealed class TransactionalDataStoreExt
        {
            private static readonly MethodInfo _storeMethodDefinition;
            private static readonly MethodInfo _removeMethodDefinition;

            private readonly ConcurrentDictionary<Type, Func<object, CancellationToken, Task>> _storeMethods;
            private readonly ConcurrentDictionary<Type, Func<object, CancellationToken, Task>> _removeMethods;
            private volatile IScopedTransactionalDatabase _database;

            static TransactionalDataStoreExt()
            {
                _storeMethodDefinition = typeof(IScopedTransactionalDatabase).GetMethods().Single(p => p.Name == nameof(IScopedTransactionalDatabase.StoreAsync));
                _removeMethodDefinition = typeof(IScopedTransactionalDatabase).GetMethods().Single(p => p.Name == nameof(IScopedTransactionalDatabase.RemoveAsync));
            }

            public TransactionalDataStoreExt()
            {
                _storeMethods = new ConcurrentDictionary<Type, Func<object, CancellationToken, Task>>();
                _removeMethods = new ConcurrentDictionary<Type, Func<object, CancellationToken, Task>>();
            }

            public void Initialize(IScopedTransactionalDatabase database)
            {
                // Volatile read op
                if (_database != null)
                {
                    return;
                }

                // Write _dataStore only if there is no data store present currently.
                var current = Interlocked.CompareExchange(ref _database, database, null);

                // If there was a data store present, it must be the same than the one, we wanted to write.
                Assert(current == null || ReferenceEquals(current, database));
            }

            public Task StoreAsync(Type dataType,
                                   object data,
                                   CancellationToken cancellation = default)
            {
                Assert(data != null);

                var function = _storeMethods.GetOrAdd(data.GetType(), BuildStoreMethod);

                Assert(function != null);

                return function(data, cancellation);
            }



            public Task RemoveAsync(Type dataType,
                                    object data,
                                    CancellationToken cancellation = default)
            {
                Assert(data != null);

                var function = _removeMethods.GetOrAdd(data.GetType(), BuildStoreMethod);

                Assert(function != null);

                return function(data, cancellation);
            }

            private Func<object, CancellationToken, Task> BuildStoreMethod(Type type)
            {
                Assert(type != null);
                var methodInfo = _storeMethodDefinition.MakeGenericMethod(type);
                return BuildMethod(type, methodInfo);
            }

            private Func<object, CancellationToken, Task> BuildRemoveMethod(Type type)
            {
                Assert(type != null);
                var methodInfo = _removeMethodDefinition.MakeGenericMethod(type);
                return BuildMethod(type, methodInfo);
            }

            private Func<object, CancellationToken, Task> BuildMethod(Type type, MethodInfo method)
            {
                Assert(type != null);
                Assert(method != null);

                // Volatile read op
                var database = _database;

                var instance = Expression.Constant(database, typeof(IScopedTransactionalDatabase));

                var dataParam = Expression.Parameter(typeof(object), "data");
                var cancellationParam = Expression.Parameter(typeof(CancellationToken), "cancellation");
                var data = Expression.Convert(dataParam, type);

                var call = Expression.Call(instance, method, data, cancellationParam);
                return Expression.Lambda<Func<object, CancellationToken, Task>>(call, dataParam, cancellationParam).Compile();
            }
        }
    }
}
