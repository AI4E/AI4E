using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using static System.Diagnostics.Debug;

namespace AI4E.Storage
{
    public static class DatabaseExtension
    {
        public static async Task UpdateAsync<TEntry>(this IDatabase database, TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            var success = await
            database.UpdateAsync(entry, _ => true, cancellation);
            Assert(success);
        }

        public static async Task RemoveAsync<TEntry>(this IDatabase database, TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            var success = await database.RemoveAsync(entry, _ => true, cancellation);
            Assert(success);
        }

        public static ValueTask<TEntry> GetOneAsync<TEntry>(this IDatabase database, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            return database.GetOneAsync<TEntry>(p => true, cancellation);
        }

        public static Task<bool> CompareExchangeAsync<TEntry, TVersion>(this IDatabase database,
                                                                             TEntry entry,
                                                                             TEntry comparand,
                                                                             Expression<Func<TEntry, TVersion>> versionSelector,
                                                                             CancellationToken cancellation = default)
            where TEntry : class
            where TVersion : IEquatable<TVersion>
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (versionSelector == null)
                throw new ArgumentNullException(nameof(versionSelector));

            return database.CompareExchangeAsync<TEntry>(entry, comparand, BuildEqualityComparer(versionSelector), cancellation);
        }

        private static Expression<Func<TEntry, TEntry, bool>> BuildEqualityComparer<TEntry, TVersion>(Expression<Func<TEntry, TVersion>> versionSelector)
            where TEntry : class
            where TVersion : IEquatable<TVersion>
        {
            Assert(versionSelector != null);

            var param1 = Expression.Parameter(typeof(TEntry), "left");
            var param2 = Expression.Parameter(typeof(TEntry), "right");

            var version1 = ParameterExpressionReplacer.ReplaceParameter(versionSelector.Body, versionSelector.Parameters.First(), param1);
            var version2 = ParameterExpressionReplacer.ReplaceParameter(versionSelector.Body, versionSelector.Parameters.First(), param2);

            var equalityMethod = typeof(IEquatable<TVersion>).GetMethod(nameof(Equals));

            Assert(equalityMethod != null);

            var call = Expression.Call(version1, equalityMethod, version2);

            Expression equality;

            if (typeof(TVersion).IsValueType)
            {
                equality = call;
            }
            else // (left == null && right == null) || (left != null && right != null && left.Equals(right));
            {
                var isNull1 = Expression.Equal(version1, Expression.Constant(null, typeof(TVersion)));
                var isNull2 = Expression.Equal(version2, Expression.Constant(null, typeof(TVersion)));
                var isNotNull1 = Expression.Not(isNull1);
                var isNotNull2 = Expression.Not(isNull2);

                var bothNull = Expression.AndAlso(isNull1, isNull2);
                var bothNotNUll = Expression.AndAlso(isNotNull1, isNotNull2);

                equality = Expression.OrElse(bothNull, Expression.AndAlso(bothNotNUll, call));
            }

            return Expression.Lambda<Func<TEntry, TEntry, bool>>(equality, param1, param2);
        }
    }
}
