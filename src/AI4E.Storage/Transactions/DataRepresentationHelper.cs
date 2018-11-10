using System;
using System.Linq;
using System.Linq.Expressions;
using AI4E.Internal;

namespace AI4E.Storage.Transactions
{
    internal static class DataRepresentationHelper
    {
        /// <summary>
        /// Checks whether the data-representation matches the specified predicate.
        /// </summary>
        /// <typeparam name="TId">The type of id.</typeparam>
        /// <typeparam name="TData">The type of data entry.</typeparam>
        /// <typeparam name="TDataRepresentation">The type of data-representation.</typeparam>
        /// <param name="dataRepresentation">The data-representation to check the condition for.</param>
        /// <param name="predicate">The predicate that describes the condition.</param>
        /// <returns>Returns <paramref name="dataRepresentation"/> if it is non-null and matches <paramref name="predicate"/>.</returns>
        public static TDataRepresentation MatchPredicate<TId, TData, TDataRepresentation>(this TDataRepresentation dataRepresentation, Func<IDataRepresentation<TId, TData>, bool> predicate)
            where TData : class
            where TDataRepresentation : class, IDataRepresentation<TId, TData>
        {
            return dataRepresentation.Data != null && predicate(dataRepresentation) ? dataRepresentation : null;
        }

        public static Expression<Func<IDataRepresentation<TId, TData>, bool>> TranslatePredicate<TId, TData>(Expression<Func<TData, bool>> predicate)
            where TData : class
        {
            return TranslatePredicate<TId, TData, IDataRepresentation<TId, TData>>(predicate);
        }

        public static Expression<Func<TDataRepresentation, bool>> TranslatePredicate<TId, TData, TDataRepresentation>(Expression<Func<TData, bool>> predicate)
            where TData : class
            where TDataRepresentation : class, IDataRepresentation<TId, TData>
        {
            // The resulting predicate checks
            // 1) If the entry's payload matches the specified predicate.
            // 2) If the entry's data is not null (The entry is not deleted)

            var parameter = Expression.Parameter(typeof(TDataRepresentation));
            var dataAccessor = DataHelper<TId, TData>.Accessor;
            var nullConstant = DataHelper<TId, TData>.Null;

            var data = ParameterExpressionReplacer.ReplaceParameter(dataAccessor.Body, dataAccessor.Parameters.First(), parameter);
            var isNull = Expression.ReferenceEqual(data, nullConstant);
            var isNotNull = Expression.Not(isNull);
            var matchesPredicate = ParameterExpressionReplacer.ReplaceParameter(predicate.Body, predicate.Parameters.First(), data);
            var body = Expression.AndAlso(isNotNull, matchesPredicate);

            return Expression.Lambda<Func<TDataRepresentation, bool>>(body, parameter);
        }

        private static class DataHelper<TId, TData>
            where TData : class
        {
            public static Expression<Func<IDataRepresentation<TId, TData>, TData>> Accessor { get; } = entry => entry.Data;
            public static ConstantExpression Null { get; } = Expression.Constant(null, typeof(TData));
        }
    }
}
