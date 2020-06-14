using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    public static partial class DatabaseScopeExtension
    {
        private static readonly MethodInfo _storeMethodDefinition = GetStoreMethodDefinition();
        private static readonly MethodInfo _removeMethodDefinition = GetRemoveMethodDefinition();

        private static readonly ConditionalWeakTable<Type, Func<IDatabaseScope, object, CancellationToken, ValueTask>> _storeMethods
                   = new ConditionalWeakTable<Type, Func<IDatabaseScope, object, CancellationToken, ValueTask>>();
        private static readonly ConditionalWeakTable<Type, Func<IDatabaseScope, object, CancellationToken, ValueTask>> _removeMethods
            = new ConditionalWeakTable<Type, Func<IDatabaseScope, object, CancellationToken, ValueTask>>();

        // Cache delegates for perf reason
        private static readonly ConditionalWeakTable<Type, Func<IDatabaseScope, object, CancellationToken, ValueTask>>.CreateValueCallback _buildStoreMethod
            = BuildStoreMethod;
        private static readonly ConditionalWeakTable<Type, Func<IDatabaseScope, object, CancellationToken, ValueTask>>.CreateValueCallback _buildRemoveMethod
            = BuildRemoveMethod;

        private static MethodInfo GetStoreMethodDefinition()
        {
            return typeof(DatabaseScopeExtension)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(p => p.Name == nameof(DatabaseScopeExtension.StoreAsync) && p.IsGenericMethodDefinition);
        }

        private static MethodInfo GetRemoveMethodDefinition()
        {
            return typeof(DatabaseScopeExtension)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(p => p.Name == nameof(DatabaseScopeExtension.RemoveAsync) && p.IsGenericMethodDefinition);
        }

        private static Func<IDatabaseScope, object, CancellationToken, ValueTask> BuildStoreMethod(Type entryType)
        {
            return BuildMethod(_storeMethodDefinition, entryType);
        }

        private static Func<IDatabaseScope, object, CancellationToken, ValueTask> BuildRemoveMethod(Type entryType)
        {
            return BuildMethod(_removeMethodDefinition, entryType);
        }

        private static Func<IDatabaseScope, object, CancellationToken, ValueTask> BuildMethod(MethodInfo methodDefinition, Type entryType)
        {
            Debug.Assert(methodDefinition.IsGenericMethodDefinition);
            Debug.Assert(methodDefinition.GetGenericArguments().Length == 1);

            var method = methodDefinition.MakeGenericMethod(entryType);

            Debug.Assert(method.ReturnType == typeof(ValueTask));
            Debug.Assert(method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new Type[] { typeof(IDatabaseScope), entryType, typeof(CancellationToken) }));

            var databaseParameter = Expression.Parameter(typeof(IDatabaseScope), "databaseScope");
            var entryParameter = Expression.Parameter(typeof(object), "entry");
            var cancellationParameter = Expression.Parameter(typeof(CancellationToken), "cancellation");
            var convertedEntry = Expression.Convert(entryParameter, entryType);
            var call = Expression.Call(method, databaseParameter, convertedEntry, cancellationParameter);
            return Expression.Lambda<Func<IDatabaseScope, object, CancellationToken, ValueTask>>(call, databaseParameter, entryParameter, cancellationParameter).Compile();
        }

        private static Func<IDatabaseScope, object, CancellationToken, ValueTask> GetStoreMethod(Type entryType)
        {
            return _storeMethods.GetValue(entryType, _buildStoreMethod);
        }

        private static Func<IDatabaseScope, object, CancellationToken, ValueTask> GetRemoveMethod(Type entryType)
        {
            return _removeMethods.GetValue(entryType, _buildRemoveMethod);
        }

        private static ValueTask StoreAsync<TEntry>(
            IDatabaseScope databaseScope,
            TEntry entry,
            CancellationToken cancellation)
            where TEntry : class
        {
            return databaseScope.StoreAsync(entry, cancellation);
        }

        private static ValueTask RemoveAsync<TEntry>(
            IDatabaseScope databaseScope,
            TEntry entry,
            CancellationToken cancellation)
            where TEntry : class
        {
            return databaseScope.RemoveAsync(entry, cancellation);
        }

        public static ValueTask StoreAsync(
          this IDatabaseScope databaseScope,
          object entry,
          CancellationToken cancellation = default)
        {
            return StoreAsync(databaseScope, entry?.GetType()!, entry!, cancellation);
        }

        public static ValueTask RemoveAsync(
            this IDatabaseScope databaseScope,
            object entry,
            CancellationToken cancellation = default)
        {
            return RemoveAsync(databaseScope, entry?.GetType()!, entry!, cancellation);
        }

        public static ValueTask StoreAsync(
            this IDatabaseScope databaseScope,
            Type entryType,
            object entry,
            CancellationToken cancellation = default)
        {
            CheckArguments(entryType, entry);
            var invoker = GetStoreMethod(entryType);
            return invoker(databaseScope, entry, cancellation);
        }

        public static ValueTask RemoveAsync(
            this IDatabaseScope databaseScope,
            Type entryType,
            object entry,
            CancellationToken cancellation = default)
        {
            CheckArguments(entryType, entry);
            var invoker = GetRemoveMethod(entryType);
            return invoker(databaseScope, entry, cancellation);
        }

        private static void CheckArguments(Type entryType, object entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entryType == null)
                throw new ArgumentNullException(nameof(entryType));

            if (entryType.IsValueType)
                throw new ArgumentException("The argument must be a reference type.", nameof(entryType));

            if (!entryType.IsAssignableFrom(entry.GetType()))
                throw new ArgumentException($"The specified entry must be of type '{entryType.FullName}' or an assignable type.");
        }
    }
}
