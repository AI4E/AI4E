/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    public class SuccessDispatchResult : DispatchResult
    {
        internal const string DefaultMessage = "Success";

        private static readonly ConcurrentDictionary<Type, Func<object, string, IReadOnlyDictionary<string, object>, SuccessDispatchResult>> _cache
            = new ConcurrentDictionary<Type, Func<object, string, IReadOnlyDictionary<string, object>, SuccessDispatchResult>>();

        private static readonly Type _successDispatchResultTypeDefinition = typeof(SuccessDispatchResult<>);

        public static SuccessDispatchResult FromResult(Type resultType, object result)
        {
            return FromResult(resultType, result, DefaultMessage, ImmutableDictionary<string, object>.Empty);
        }

        public static SuccessDispatchResult FromResult(Type resultType, object result, string message, IReadOnlyDictionary<string, object> resultData)
        {
            if (resultType == null)
                throw new ArgumentNullException(nameof(resultType));

            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (resultData == null)
                throw new ArgumentNullException(nameof(resultData));

            if (!resultType.IsAssignableFrom(result.GetType()))
                throw new ArgumentException("The result type must be asignable to the type specified.");

            var builder = _cache.GetOrAdd(resultType, BuildDispatchResultBuilder);
            return builder(result, message, resultData);
        }

        public static SuccessDispatchResult FromResult(object result)
        {
            return FromResult(result.GetType(), result, DefaultMessage, ImmutableDictionary<string, object>.Empty);
        }

        public static SuccessDispatchResult FromResult(object result, string message, IReadOnlyDictionary<string, object> resultData)
        {
            return FromResult(result.GetType(), result, message, resultData);
        }

        // TODO: Remove this in favor of forcing the caller to invoke the constructor of the generic type directly?
        public static SuccessDispatchResult<TResult> FromResult<TResult>(TResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            return new SuccessDispatchResult<TResult>(result);
        }

        // TODO: Remove this in favor of forcing the caller to invoke the constructor of the generic type directly?
        public static SuccessDispatchResult<TResult> FromResult<TResult>(TResult result, string message, IReadOnlyDictionary<string, object> resultData)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (resultData == null)
                throw new ArgumentNullException(nameof(resultData));

            return new SuccessDispatchResult<TResult>(result, message, resultData);
        }

        private static Func<object, string, IReadOnlyDictionary<string, object>, SuccessDispatchResult> BuildDispatchResultBuilder(Type resultType)
        {
            if (resultType.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be a generic type definition.", nameof(resultType));

            var type = _successDispatchResultTypeDefinition.MakeGenericType(resultType);
            var ctor = type.GetConstructor(new Type[] { resultType, typeof(string), typeof(IReadOnlyDictionary<string, object>) });

            Debug.Assert(ctor != null);

            var resultParameter = Expression.Parameter(typeof(object), "result");
            var messageParameter = Expression.Parameter(typeof(string), "message");
            var resultDataParameter = Expression.Parameter(typeof(IReadOnlyDictionary<string, object>), "resultData");
            var convertedResult = Expression.Convert(resultParameter, resultType);
            var ctorCall = Expression.New(ctor, convertedResult, messageParameter, resultDataParameter);
            var convertedDispatchResult = Expression.Convert(ctorCall, typeof(SuccessDispatchResult));
            return Expression.Lambda<Func<object, string, IReadOnlyDictionary<string, object>, SuccessDispatchResult>>(
                convertedDispatchResult,
                resultParameter,
                messageParameter,
                resultDataParameter).Compile();
        }

        [JsonConstructor]
        public SuccessDispatchResult(string message, IReadOnlyDictionary<string, object> resultData)
            : base(true, message, resultData) { }

        public SuccessDispatchResult(string message)
            : this(message, ImmutableDictionary<string, object>.Empty) { }

        public SuccessDispatchResult()
            : this(DefaultMessage) { }

        [JsonIgnore]
        public override bool IsSuccess => true;
    }

    public class SuccessDispatchResult<TResult> : SuccessDispatchResult, IDispatchResult<TResult>
    {
        [JsonConstructor]
        public SuccessDispatchResult(TResult result, string message, IReadOnlyDictionary<string, object> resultData) : base(message, resultData)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Result = result;
        }

        public SuccessDispatchResult(TResult result, string message) : base(message)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Result = result;
        }

        public SuccessDispatchResult(TResult result) : this(result, DefaultMessage) { }

        public TResult Result { get; }

        protected override void FormatString(StringBuilder stringBuilder)
        {
            base.FormatString(stringBuilder);

            stringBuilder.Append("[Result: ");
            stringBuilder.Append(Result.ToString());
            stringBuilder.Append("]");
        }
    }
}
