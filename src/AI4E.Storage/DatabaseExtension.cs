using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
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

        public async static Task RemoveAsync<TEntry>(this IDatabase database, TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            var success = await database.RemoveAsync(entry, _ => true, cancellation);
            Assert(success);
        }

        public static ValueTask<bool> CompareExchangeAsync<TEntry, TVersion>(this IDatabase database,
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

        //public static ValueTask<bool> CompareExchangeAsync<TEntry, TVersion>(this IDatabase database,
        //                                                                     TEntry data,
        //                                                                     TEntry comparand,
        //                                                                     Expression<Func<TEntry, TVersion>> versionSelector,
        //                                                                     CancellationToken cancellation = default)
        //    where TEntry : class
        //    where TVersion : struct, IEquatable<TVersion>
        //{
        //    if (database == null)
        //        throw new ArgumentNullException(nameof(database));

        //    if (versionSelector == null)
        //        throw new ArgumentNullException(nameof(versionSelector));

        //    // This is a nop actually. But we check whether comparand is up to date.
        //    if (data == comparand)
        //    {
        //        return CheckComparandToBeUpToDate(database, comparand, versionSelector, cancellation);
        //    }

        //    // Trying to update an entry.
        //    if (data != null && comparand != null)
        //    {
        //        return database.UpdateAsync(data, BuildPredicate(comparand, versionSelector), cancellation);
        //    }

        //    // Trying to create an entry.
        //    if (data != null)
        //    {
        //        return database.InsertAsync(data, cancellation);
        //    }

        //    // Trying to remove an entry.
        //    Assert(comparand != null);

        //    return database.RemoveAsync(comparand, BuildPredicate(comparand, versionSelector), cancellation);
        //}

        //private static async ValueTask<bool> CheckComparandToBeUpToDate<TEntry, TVersion>(IDatabase database,
        //                                                                                  TEntry comparand,
        //                                                                                  Expression<Func<TEntry, TVersion>> versionSelector,
        //                                                                                  CancellationToken cancellation)
        //    where TEntry : class
        //    where TVersion : struct, IEquatable<TVersion>
        //{
        //    var result = (await database.GetAsync(DataPropertyHelper.BuildPredicate(comparand), cancellation)).FirstOrDefault();

        //    if (comparand == null)
        //    {
        //        return result == null;
        //    }

        //    var compiledVersionSelector = versionSelector.Compile(preferInterpretation: true);
        //    return compiledVersionSelector(comparand).Equals(compiledVersionSelector(result));
        //}

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
