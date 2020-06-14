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
    public static partial class DatabaseExtension
    {
        private static readonly MethodInfo _addMethodDefinition = GetAddMethodDefinition();
        private static readonly MethodInfo _updateMethodDefinition = GetUpdateMethodDefinition();
        private static readonly MethodInfo _removeMethodDefinition = GetRemoveMethodDefinition();

        private static readonly ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>> _addMethods
                   = new ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>>();
        private static readonly ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>> _updateMethods
            = new ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>>();
        private static readonly ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>> _removeMethods
            = new ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>>();

        // Caching delegates for perf reason
        private static readonly ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>>.CreateValueCallback _buildAddMethod
            = BuildAddMethod;
        private static readonly ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>>.CreateValueCallback _buildUpdateMethod
            = BuildUpdateMethod;
        private static readonly ConditionalWeakTable<Type, Func<IDatabase, object, CancellationToken, ValueTask<bool>>>.CreateValueCallback _buildRemoveMethod
            = BuildRemoveMethod;

        private static MethodInfo GetAddMethodDefinition()
        {
            return typeof(DatabaseExtension)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(p => p.Name == nameof(DatabaseExtension.AddAsync) && p.IsGenericMethodDefinition);
        }

        private static MethodInfo GetUpdateMethodDefinition()
        {
            return typeof(DatabaseExtension)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(p => p.Name == nameof(DatabaseExtension.UpdateAsync) && p.IsGenericMethodDefinition);
        }

        private static MethodInfo GetRemoveMethodDefinition()
        {
            return typeof(DatabaseExtension)
                 .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                 .Single(p => p.Name == nameof(DatabaseExtension.RemoveAsync) && p.IsGenericMethodDefinition);
        }

        private static Func<IDatabase, object, CancellationToken, ValueTask<bool>> BuildAddMethod(Type dataType)
        {
            return BuildMethod(_addMethodDefinition, dataType);
        }

        private static Func<IDatabase, object, CancellationToken, ValueTask<bool>> BuildUpdateMethod(Type dataType)
        {
            return BuildMethod(_updateMethodDefinition, dataType);
        }

        private static Func<IDatabase, object, CancellationToken, ValueTask<bool>> BuildRemoveMethod(Type dataType)
        {
            return BuildMethod(_removeMethodDefinition, dataType);
        }

        private static Func<IDatabase, object, CancellationToken, ValueTask<bool>> BuildMethod(MethodInfo methodDefinition, Type dataType)
        {
            Debug.Assert(methodDefinition.IsGenericMethodDefinition);
            Debug.Assert(methodDefinition.GetGenericArguments().Length == 1);

            var method = methodDefinition.MakeGenericMethod(dataType);

            Debug.Assert(method.ReturnType == typeof(ValueTask<bool>));
            Debug.Assert(method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new Type[] { typeof(IDatabase), dataType, typeof(CancellationToken) }));

            var databaseParameter = Expression.Parameter(typeof(IDatabase), "database");
            var entryParameter = Expression.Parameter(typeof(object), "entry");
            var cancellationParameter = Expression.Parameter(typeof(CancellationToken), "cancellation");
            var convertedEntry = Expression.Convert(entryParameter, dataType);
            var call = Expression.Call(method, databaseParameter, convertedEntry, cancellationParameter);
            return Expression.Lambda<Func<IDatabase, object, CancellationToken, ValueTask<bool>>>(call, databaseParameter, entryParameter, cancellationParameter).Compile();
        }

        private static Func<IDatabase, object, CancellationToken, ValueTask<bool>> GetAddMethod(Type dataType)
        {
            return _addMethods.GetValue(dataType, _buildAddMethod);
        }

        private static Func<IDatabase, object, CancellationToken, ValueTask<bool>> GetUpdateMethod(Type dataType)
        {
            return _updateMethods.GetValue(dataType, _buildUpdateMethod);
        }

        private static Func<IDatabase, object, CancellationToken, ValueTask<bool>> GetRemoveMethod(Type dataType)
        {
            return _removeMethods.GetValue(dataType, _buildRemoveMethod);
        }

        private static ValueTask<bool> AddAsync<TEntry>(IDatabase database, TEntry entry, CancellationToken cancellation)
             where TEntry : class
        {
            return database.AddAsync(entry, cancellation);
        }

        private static ValueTask<bool> UpdateAsync<TEntry>(IDatabase database, TEntry entry, CancellationToken cancellation)
            where TEntry : class
        {
            return database.UpdateAsync(entry, _ => true, cancellation);
        }

        private static ValueTask<bool> RemoveAsync<TEntry>(IDatabase database, TEntry entry, CancellationToken cancellation)
           where TEntry : class
        {
            return database.RemoveAsync(entry, _ => true, cancellation);
        }

        /// <summary>
        /// Stores an object in the store.
        /// </summary>
        /// <param name="database">The data store.</param>
        /// <param name="entry">The object to update.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="database"/> or <paramref name="entry"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        public static ValueTask<bool> AddAsync(this IDatabase database, object entry, CancellationToken cancellation = default)
        {
            return database.AddAsync(entry?.GetType()!, entry!, cancellation);
        }

        public static ValueTask<bool> UpdateAsync(this IDatabase database, object entry, CancellationToken cancellation = default)
        {
            return database.UpdateAsync(entry?.GetType()!, entry!, cancellation);
        }

        /// <summary>
        /// Removes an object from the store.
        /// </summary>
        /// <param name="database">The data store.</param>
        /// <param name="entry">The object to remove.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="database"/> or <paramref name="entry"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        public static ValueTask<bool> RemoveAsync(this IDatabase database, object entry, CancellationToken cancellation = default)
        {
            return database.RemoveAsync(entry?.GetType()!, entry!, cancellation);
        }

        public static ValueTask<bool> AddAsync(this IDatabase database, Type entryType, object data, CancellationToken cancellation = default)
        {
            CheckArguments(entryType, data);
            var invoker = GetAddMethod(entryType);
            return invoker(database, data, cancellation);
        }

        public static ValueTask<bool> UpdateAsync(this IDatabase database, Type entryType, object entry, CancellationToken cancellation = default)
        {
            CheckArguments(entryType, entry);
            var invoker = GetUpdateMethod(entryType);
            return invoker(database, entry, cancellation);
        }

        public static ValueTask<bool> RemoveAsync(this IDatabase database, Type entryType, object entry, CancellationToken cancellation = default)
        {
            CheckArguments(entryType, entry);
            var invoker = GetRemoveMethod(entryType);
            return invoker(database, entry, cancellation);
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
                throw new ArgumentException($"The specified data must be of type '{entryType.FullName}' or an assignable type.");
        }
    }
}
