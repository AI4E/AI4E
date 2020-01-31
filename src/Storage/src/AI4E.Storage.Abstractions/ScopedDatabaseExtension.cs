using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    public static class ScopedDatabaseExtension
    {
        private static readonly MethodInfo _storeMethodDefinition;
        private static readonly MethodInfo _removeMethodDefinition;

        static ScopedDatabaseExtension()
        {
            _storeMethodDefinition = typeof(ScopedDatabaseExtension)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(p => p.Name == nameof(ScopedDatabaseExtension.StoreAsync) &&
                             p.IsGenericMethodDefinition);

            _removeMethodDefinition = typeof(ScopedDatabaseExtension)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(p => p.Name == nameof(ScopedDatabaseExtension.RemoveAsync) &&
                             p.IsGenericMethodDefinition);
        }

        private static readonly ConcurrentDictionary<Type, Func<IScopedDatabase, object, CancellationToken, Task>> _storeMethods = new ConcurrentDictionary<Type, Func<IScopedDatabase, object, CancellationToken, Task>>();
        private static readonly ConcurrentDictionary<Type, Func<IScopedDatabase, object, CancellationToken, Task>> _removeMethods = new ConcurrentDictionary<Type, Func<IScopedDatabase, object, CancellationToken, Task>>();

        private static readonly Func<Type, Func<IScopedDatabase, object, CancellationToken, Task>> _buildStoreMethodCache = BuildStoreMethod;
        private static readonly Func<Type, Func<IScopedDatabase, object, CancellationToken, Task>> _buildRemoveMethodCache = BuildRemoveMethod;

        private static Func<IScopedDatabase, object, CancellationToken, Task> GetStoreMethod(Type dataType)
        {
            return _storeMethods.GetOrAdd(dataType, _buildStoreMethodCache);
        }

        private static Func<IScopedDatabase, object, CancellationToken, Task> GetRemoveMethod(Type dataType)
        {
            return _removeMethods.GetOrAdd(dataType, _buildRemoveMethodCache);
        }


        private static Func<IScopedDatabase, object, CancellationToken, Task> BuildMethod(MethodInfo methodDefinition, Type dataType)
        {
            Debug.Assert(methodDefinition.IsGenericMethodDefinition);
            Debug.Assert(methodDefinition.GetGenericArguments().Length == 1);

            var method = methodDefinition.MakeGenericMethod(dataType);

            Debug.Assert(method.ReturnType == typeof(Task));
            Debug.Assert(method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new Type[] { typeof(IScopedDatabase), dataType, typeof(CancellationToken) }));

            var databaseParameter = Expression.Parameter(typeof(IScopedDatabase), "scopedDatabase");
            var entryParameter = Expression.Parameter(typeof(object), "data");
            var cancellationParameter = Expression.Parameter(typeof(CancellationToken), "cancellation");
            var convertedEntry = Expression.Convert(entryParameter, dataType);
            var call = Expression.Call(method, databaseParameter, convertedEntry, cancellationParameter);
            return Expression.Lambda<Func<IScopedDatabase, object, CancellationToken, Task>>(call, databaseParameter, entryParameter, cancellationParameter).Compile();
        }

        private static Func<IScopedDatabase, object, CancellationToken, Task> BuildStoreMethod(Type dataType)
        {
            return BuildMethod(_storeMethodDefinition, dataType);
        }

        private static Func<IScopedDatabase, object, CancellationToken, Task> BuildRemoveMethod(Type dataType)
        {
            return BuildMethod(_removeMethodDefinition, dataType);
        }

        private static Task StoreAsync<TData>(
            IScopedDatabase scopedDatabase,
            TData data,
            CancellationToken cancellation)
            where TData : class
        {
            return scopedDatabase.StoreAsync(data, cancellation);
        }

        private static Task RemoveAsync<TData>(
            IScopedDatabase scopedDatabase,
            TData data,
            CancellationToken cancellation)
            where TData : class
        {
            return scopedDatabase.RemoveAsync(data, cancellation);
        }

        public static Task StoreAsync(
            this IScopedDatabase scopedDatabase,
            Type dataType,
            object data,
            CancellationToken cancellation = default)
        {
            CheckArguments(dataType, data);

            var invoker = GetStoreMethod(dataType);
            return invoker(scopedDatabase, data, cancellation);
        }

        public static Task RemoveAsync(
            this IScopedDatabase scopedDatabase,
            Type dataType,
            object data,
            CancellationToken cancellation = default)
        {
            CheckArguments(dataType, data);
            var invoker = GetRemoveMethod(dataType);
            return invoker(scopedDatabase, data, cancellation);
        }

        private static void CheckArguments(Type dataType, object data)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (dataType.IsValueType)
                throw new ArgumentException("The argument must be a reference type.", nameof(dataType));

            if (!dataType.IsAssignableFrom(data.GetType()))
                throw new ArgumentException($"The specified data must be of type '{dataType.FullName}' or an assignable type.");
        }
    }
}
