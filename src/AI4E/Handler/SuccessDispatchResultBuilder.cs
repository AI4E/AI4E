using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using AI4E.DispatchResults;

namespace AI4E.Handler
{
    public static class SuccessDispatchResultBuilder
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IDispatchResult>> _cache
            = new ConcurrentDictionary<Type, Func<object, IDispatchResult>>();

        private static readonly Type _successDispatchResultTypeDefinition = typeof(SuccessDispatchResult<>);

        public static IDispatchResult GetSuccessDispatchResult(Type resultType, object result)
        {
            if (resultType == null)
                throw new ArgumentNullException(nameof(resultType));

            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var builder = _cache.GetOrAdd(resultType, BuildDispatchResultBuilder);
            return builder(result);
        }

        private static Func<object, IDispatchResult> BuildDispatchResultBuilder(Type resultType)
        {
            if (resultType.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be a generic type definition.", nameof(resultType));

            var type = _successDispatchResultTypeDefinition.MakeGenericType(resultType);
            var ctor = _successDispatchResultTypeDefinition.GetConstructor(new Type[] { resultType });

            Debug.Assert(ctor != null);

            var resultParameter = Expression.Parameter(typeof(object));
            var convertedResultParameter = Expression.Convert(resultParameter, resultType);
            var ctorCall = Expression.New(ctor, convertedResultParameter);
            var convertedDispatchResult = Expression.Convert(ctorCall, typeof(IDispatchResult));
            return Expression.Lambda<Func<object, IDispatchResult>>(convertedDispatchResult, resultParameter).Compile();
        }
    }
}
