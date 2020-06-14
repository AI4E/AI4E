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

namespace AI4E.Messaging
{
    /// <summary>
    /// Describes the result of a succesful message dispatch operation.
    /// </summary>
    public class SuccessDispatchResult : DispatchResult
    {
        public static string DefaultMessage { get; } = "Success";

        #region Factory methods

        private static readonly ConcurrentDictionary<Type, Func<object, string, IReadOnlyDictionary<string, object>, SuccessDispatchResult>> _cache
            = new ConcurrentDictionary<Type, Func<object, string, IReadOnlyDictionary<string, object>, SuccessDispatchResult>>();

        private static readonly Type _successDispatchResultTypeDefinition = typeof(SuccessDispatchResult<>);

        /// <summary>
        /// Creates a <see cref="SuccessDispatchResult"/> from the specified result value.
        /// </summary>
        /// <param name="resultType">The type of result value.</param>
        /// <param name="result">The result value.</param>
        /// <returns>The created <see cref="SuccessDispatchResult"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="resultType"/> or <paramref name="result"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the type of <paramref name="result"/> is not assignable to <paramref name="resultType"/>.
        /// </exception>
        public static SuccessDispatchResult FromResult(Type resultType, object result)
        {
            return FromResult(resultType, result, DefaultMessage, ImmutableDictionary<string, object>.Empty);
        }

        /// <summary>
        /// Creates a <see cref="SuccessDispatchResult"/> from the specified result value.
        /// </summary>
        /// <param name="resultType">The type of result value.</param>
        /// <param name="result">The result value.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <returns>The created <see cref="SuccessDispatchResult"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="resultType"/>, <paramref name="result"/>,
        /// <paramref name="message"/> or <paramref name="resultData"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the type of <paramref name="result"/> is not assignable to <paramref name="resultType"/>.
        /// </exception>
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

        /// <summary>
        /// Creates a <see cref="SuccessDispatchResult"/> from the specified result value.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <returns>The created <see cref="SuccessDispatchResult"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="result"/> is null.</exception>
        public static SuccessDispatchResult FromResult(object result)
        {
            return FromResult(result?.GetType()!, result!, DefaultMessage, ImmutableDictionary<string, object>.Empty);
        }

        /// <summary>
        /// Creates a <see cref="SuccessDispatchResult"/> from the specified result value.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <returns>The created <see cref="SuccessDispatchResult"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="result"/>,
        /// <paramref name="message"/> or <paramref name="resultData"/> is null.
        /// </exception>
        public static SuccessDispatchResult FromResult(object result, string message, IReadOnlyDictionary<string, object> resultData)
        {
            return FromResult(result?.GetType()!, result!, message, resultData);
        }

        /// <summary>
        /// Creates a <see cref="SuccessDispatchResult"/> from the specified result value.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <typeparam name="TResult">The type of result value.</typeparam>
        /// <returns>The created <see cref="SuccessDispatchResult"/>.</returns>
        /// <exception cref="ArgumentNullException"> Thrown if <paramref name="result"/> is null. </exception>
        public static SuccessDispatchResult<TResult> FromResult<TResult>(TResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            return new SuccessDispatchResult<TResult>(result);
        }

        /// <summary>
        /// Creates a <see cref="SuccessDispatchResult"/> from the specified result value.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <typeparam name="TResult">The type of result value.</typeparam>
        /// <returns>The created <see cref="SuccessDispatchResult"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="result"/>,
        /// <paramref name="message"/> or <paramref name="resultData"/> is null.
        /// </exception>
        public static SuccessDispatchResult<TResult> FromResult<TResult>(TResult result, string message, IReadOnlyDictionary<string, object?> resultData)
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

        #endregion

        #region C'tors

        /// <summary>
        /// Creates a new instance of the <see cref="SuccessDispatchResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        [JsonConstructor]
        public SuccessDispatchResult(string message, IReadOnlyDictionary<string, object?> resultData)
            : base(true, message, resultData) { }

        /// <summary>
        /// Creates a new instance of the <see cref="SuccessDispatchResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        public SuccessDispatchResult(string message)
            : this(message, ImmutableDictionary<string, object?>.Empty) { }

        /// <summary>
        /// Creates a new instance of the <see cref="SuccessDispatchResult"/> type.
        /// </summary>
        public SuccessDispatchResult()
            : this(DefaultMessage) { }

        #endregion

        /// <inheritdoc />
        [JsonIgnore]
        public override bool IsSuccess => true;
    }

    /// <summary>
    /// Describes the result of a succesful message dispatch operation with a result value.
    /// </summary>
    /// <typeparam name="TResult">The type of result value.</typeparam>
    public class SuccessDispatchResult<TResult> : SuccessDispatchResult, IDispatchResult<TResult>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="SuccessDispatchResult{TResult}"/> type.
        /// </summary>
        /// <param name="result">The dispatch result value.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="result"/>, <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        [JsonConstructor]
        public SuccessDispatchResult(TResult result, string message, IReadOnlyDictionary<string, object?> resultData) : base(message, resultData)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Result = result;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SuccessDispatchResult{TResult}"/> type.
        /// </summary>
        /// <param name="result">The dispatch result value.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="result"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        public SuccessDispatchResult(TResult result, string message) : base(message)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Result = result;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SuccessDispatchResult{TResult}"/> type.
        /// </summary>
        /// <param name="result">The dispatch result value.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="result"/> is <c>null</c>.</exception>
        public SuccessDispatchResult(TResult result) : this(result, DefaultMessage) { }

        /// <inheritdoc />
        public TResult Result { get; }

        /// <inheritdoc />
        protected override void FormatString(StringBuilder stringBuilder)
        {
            base.FormatString(stringBuilder);

            stringBuilder.Append("[Result: ");
            stringBuilder.Append(Result!.ToString());
            stringBuilder.Append("]");
        }
    }
}
