using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;

namespace AI4E.Storage
{
    public static partial class DatabaseExtension
    {
        public static ValueTask<bool> CompareExchangeAsync<TEntry>(
            this IDatabase database,
            TEntry? entry,
            TEntry? comparand,
            Expression<Func<TEntry, bool>> entrySelector,
            Expression<Func<TEntry, TEntry, bool>> equalityComparer,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entrySelector is null)
                throw new ArgumentNullException(nameof(entrySelector));

            if (equalityComparer is null)
                throw new ArgumentNullException(nameof(equalityComparer));

            // This is a nop actually. But we check whether comparand is up to date.
            // This includes the case, that both are null.
            if (entry == comparand)
            {
                return CheckComparandToBeUpToDateAsync(
#pragma warning disable CA1062
                    database, comparand, entrySelector, equalityComparer, cancellation);
#pragma warning restore CA1062
            }

            if (comparand is null)
            {
                Debug.Assert(entry != null);
                return database.AddAsync(entry!, cancellation);
            }

            var predicate = BuildPredicate(comparand, entrySelector, equalityComparer);

            if (entry is null)
            {
                return database.RemoveAsync(comparand, predicate, cancellation);
            }

            return database.UpdateAsync(entry, predicate, cancellation);
        }

        /// <summary>
        /// Asynchronously replaces an entry with the specified one if the existing entry equals the specified comparand.
        /// </summary>
        /// <typeparam name="TEntry">The type of entry.</typeparam>
        /// <param name="entry">The entry to insert on succcess.</param>
        /// <param name="comparand">The comparand entry.</param>
        /// <param name="equalityComparer">An expression that specifies the equality of two entries,</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the entry was inserted successfully.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="equalityComparer"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Throw if either both, <paramref name="entry"/> and <paramref name="comparand"/> are null or 
        ///       if both are non null and the id of <paramref name="entry"/> does not match the id of <paramref name="comparand"/>. 
        /// </exception>
        /// <exception cref="StorageException">Thrown if an unresolvable exception occurs in the storage subsystem.</exception>
        /// <exception cref="StorageUnavailableException">Thrown if the storage subsystem is unavailable or unreachable.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the database does not support the specified equality comparer.</exception>
        [Obsolete]
        public static ValueTask<bool> CompareExchangeAsync<TEntry>(
            this IDatabase database,
            TEntry? entry,
            TEntry? comparand,
            Expression<Func<TEntry, TEntry, bool>> equalityComparer,
            CancellationToken cancellation = default)
           where TEntry : class
        {
            return CompareExchangeAsync(
                database,
                entry,
                comparand,
                DataPropertyHelper.BuildPredicate(comparand ?? entry ?? throw new ArgumentException()),
                equalityComparer,
                cancellation);
        }

        private static async ValueTask<bool> CheckComparandToBeUpToDateAsync<TEntry>(
            IDatabase database,
            TEntry? comparand,
            Expression<Func<TEntry, bool>> entrySelector,
            Expression<Func<TEntry, TEntry, bool>> equalityComparer,
            CancellationToken cancellation)
            where TEntry : class
        {
            var result = await database.GetOneAsync(entrySelector, cancellation);

            if (comparand is null)
            {
                return result is null;
            }

            if (result is null)
            {
                return false;
            }

            return equalityComparer.Compile(preferInterpretation: true).Invoke(comparand, result);
        }

        private static Expression<Func<TEntry, bool>> BuildPredicate<TEntry>(
            TEntry comparand,
            Expression<Func<TEntry, bool>> entrySelector,
            Expression<Func<TEntry, TEntry, bool>> equalityComparer)
            where TEntry : class
        {
            Debug.Assert(comparand != null);
            Debug.Assert(entrySelector != null);
            Debug.Assert(equalityComparer != null);

            var comparandConstant = Expression.Constant(comparand, typeof(TEntry));
            var parameter = equalityComparer!.Parameters.First();
            var equalityComparerPass = ParameterExpressionReplacer.ReplaceParameter(equalityComparer.Body, equalityComparer.Parameters.Last(), comparandConstant);
            var entrySelectorPass = ParameterExpressionReplacer.ReplaceParameter(entrySelector!.Body, entrySelector.Parameters.First(), parameter);
            var body = Expression.AndAlso(entrySelectorPass, equalityComparerPass);

            return Expression.Lambda<Func<TEntry, bool>>(body, parameter);
        }
    }
}
