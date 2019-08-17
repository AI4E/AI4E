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
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using AI4E.DispatchResults;

namespace AI4E
{
    /// <summary>
    /// Contains extensions for the <see cref="IDispatchResult"/> type.
    /// </summary>
    public static partial class DispatchResultExtension
    {
        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result is a not-authorized dispatch result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>True if <paramref name="dispatchResult"/> is a not-authorized result, false otherwise.</returns>
        public static bool IsNotAuthorized(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is NotAuthorizedDispatchResult);
            }

            return dispatchResult is NotAuthorizedDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a not-authenticated dispatch result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is a not-authenticated result, false otherwise.
        /// </returns>
        public static bool IsNotAuthenticated(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is NotAuthenticatedDispatchResult);
            }

            return dispatchResult is NotAuthenticatedDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a validation failure dispatch result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is a validation failure result, false otherwise.
        /// </returns>
        public static bool IsValidationFailed(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(
                    p => p is ValidationFailureDispatchResult);
            }

            return dispatchResult is ValidationFailureDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a validation failure dispatch result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="validationResults">Contains the validation results if the operation returns <c>true</c>.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is a validation failure result, false otherwise.
        /// </returns>
        public static bool IsValidationFailed(
            this IDispatchResult dispatchResult, out IEnumerable<ValidationResult> validationResults)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                var innerResults = aggregateDispatchResult.Flatten().DispatchResults
                    .OfType<ValidationFailureDispatchResult>();

                if (!innerResults.Any())
                {
                    validationResults = Enumerable.Empty<ValidationResult>();
                    return false;
                }

                validationResults = innerResults.SelectMany(p => p.ValidationResults);
                return true;
            }

            if (dispatchResult is ValidationFailureDispatchResult validationFailureDispatchResult)
            {
                validationResults = validationFailureDispatchResult.ValidationResults;
                return true;
            }

            validationResults = Enumerable.Empty<ValidationResult>();
            return false;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a concurrency issue result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is a concurrency issue result, false otherwise.
        /// </returns>
        public static bool IsConcurrencyIssue(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(
                    p => p is ConcurrencyIssueDispatchResult);
            }

            return dispatchResult is ConcurrencyIssueDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is an entity-not-found result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is an entity-not-found result, false otherwise.
        /// </returns>
        public static bool IsEntityNotFound(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(
                    p => p is EntityNotFoundDispatchResult);
            }

            return dispatchResult is EntityNotFoundDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is an entity-not-found result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="entityType">
        /// Contains type of the entity that could not be found if the operation returns true.
        /// </param>
        /// <param name="id">
        /// Contains the id of the entity that could not be found if the operation returns true.
        /// </param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is an entity-not-found result, false otherwise.
        /// </returns>
        public static bool IsEntityNotFound(
            this IDispatchResult dispatchResult, out Type entityType, out string id)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                dispatchResult = aggregateDispatchResult.Flatten()
                                                        .DispatchResults
                                                        .OfType<EntityNotFoundDispatchResult>()
                                                        .FirstOrDefault();
            }

            if (dispatchResult is EntityNotFoundDispatchResult entityNotFoundDispatchResult)
            {
                if (!entityNotFoundDispatchResult.TryGetEntityType(out entityType))
                {
                    entityType = null;
                }

                id = entityNotFoundDispatchResult.Id;
                return true;
            }

            entityType = default;
            id = default;

            return false;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is an entity-already-present result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is an entity-already-present result, false otherwise.
        /// </returns>
        public static bool IsEntityAlreadyPresent(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(
                    p => p is EntityAlreadyPresentDispatchResult);
            }

            return dispatchResult is EntityAlreadyPresentDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is an entity-already-present result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="entityType">
        /// Contains the unqualified type name of the entity thats id conflicted if the operation returns true.
        /// </param>
        /// <param name="id">
        /// Contains the conflicting entity id if the operation returns true.
        /// </param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is an entity-already-present result, false otherwise.
        /// </returns>
        public static bool IsEntityAlreadyPresent(
            this IDispatchResult dispatchResult, out Type entityType, out string id)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                dispatchResult = aggregateDispatchResult.Flatten()
                                                        .DispatchResults
                                                        .OfType<EntityAlreadyPresentDispatchResult>()
                                                        .FirstOrDefault();
            }

            if (dispatchResult is EntityAlreadyPresentDispatchResult entityAlreadyPresentDispatchResult)
            {
                if (!entityAlreadyPresentDispatchResult.TryGetEntityType(out entityType))
                {
                    entityType = null;
                }

                id = entityAlreadyPresentDispatchResult.Id;
                return true;
            }

            entityType = default;
            id = default;

            return false;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a timeout result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is a timeout result, false otherwise.
        /// </returns>
        public static bool IsTimeout(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(
                    p => p is TimeoutDispatchResult);
            }

            return dispatchResult is TimeoutDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a timeout result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="dueTime">Contains the due time if the operation returns true.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is a timeout result, false otherwise.
        /// </returns>
        public static bool IsTimeout(this IDispatchResult dispatchResult, out DateTime? dueTime)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                dispatchResult = aggregateDispatchResult.Flatten()
                                                        .DispatchResults
                                                        .OfType<TimeoutDispatchResult>()
                                                        .FirstOrDefault();
            }

            if (dispatchResult is TimeoutDispatchResult timeoutDispatchResult)
            {
                dueTime = timeoutDispatchResult.DueTime;
                return true;
            }

            dueTime = default;
            return false;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is an aggregate result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is an aggregate result, false otherwise.
        /// </returns>
        public static bool IsAggregateResult(this IDispatchResult dispatchResult)
        {
            return dispatchResult is IAggregateDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is an aggregate result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="aggregateDispatchResult">Contains the aggregate result if the operation returns true.</param>
        /// <returns>
        /// True if <paramref name="dispatchResult"/> is an aggregate result, false otherwise.
        /// </returns>
        public static bool IsAggregateResult(
            this IDispatchResult dispatchResult, out IAggregateDispatchResult aggregateDispatchResult)
        {
            if (dispatchResult is IAggregateDispatchResult aggregateDispatchResult2)
            {
                aggregateDispatchResult = aggregateDispatchResult2;
                return true;
            }

            aggregateDispatchResult = default;
            return false;
        }

        /// <summary>
        /// Flattens an aggregate dispatch result.
        /// </summary>
        /// <param name="aggregateDispatchResult">The aggregate dispatch result.</param>
        /// <returns>The flattened aggregate dispatch result.</returns>
        public static IAggregateDispatchResult Flatten(this IAggregateDispatchResult aggregateDispatchResult)
        {
            if (aggregateDispatchResult == null)
                throw new ArgumentNullException(nameof(aggregateDispatchResult));

            var combinedDispatchResultData = aggregateDispatchResult.ResultData;
            var dispatchResults = new List<IDispatchResult>();

            FlattenInternal(aggregateDispatchResult, dispatchResults, out var needsFlattening);

            if (!needsFlattening)
            {
                return aggregateDispatchResult;
            }

            return new AggregateDispatchResult(dispatchResults, combinedDispatchResultData);
        }

        private static void FlattenInternal(
            IAggregateDispatchResult aggregateDispatchResult,
            List<IDispatchResult> dispatchResults,
            out bool needsFlattening)
        {
            needsFlattening = false;

            foreach (var child in aggregateDispatchResult.DispatchResults)
            {
                if (child is IAggregateDispatchResult childAggregateDispatchResult)
                {
                    needsFlattening = true;
                    FlattenInternal(childAggregateDispatchResult, dispatchResults, out _);
                }
                else
                {
                    dispatchResults.Add(child);
                }
            }
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a not-found result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>True if <paramref name="dispatchResult"/> is a not-found result, false otherwise.</returns>
        public static bool IsNotFound(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(
                    p => p is NotFoundDispatchResult);
            }

            return dispatchResult is NotFoundDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a success result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="result">Contains the result value or <c>default</c>.</param>
        /// <returns>True if <paramref name="dispatchResult"/> is a success result, false otherwise.</returns>
        public static bool IsSuccess(this IDispatchResult dispatchResult, out object result)
        {
            return dispatchResult.IsSuccess<object>(out result);
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a success result.
        /// </summary>
        /// <typeparam name="TResult">The expected type of result value.</typeparam>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="result">Contains the result value or <c>default</c>.</param>
        /// <returns>True if <paramref name="dispatchResult"/> is a success result, false otherwise.</returns>
        public static bool IsSuccess<TResult>(this IDispatchResult dispatchResult, out TResult result)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                var dispatchResults = aggregateDispatchResult.Flatten()
                                                             .DispatchResults
                                                             .Where(p => p.IsSuccess);

                result = default;
                if (dispatchResults.Any())
                {
                    foreach (var dR in dispatchResults)
                    {
                        if (TryGetResult(dR, out result))
                        {
                            return true;
                        }
                    }

                    result = default;
                    return true;
                }

                return false;
            }

            if (dispatchResult.IsSuccess)
            {
                if (!TryGetResult(dispatchResult, out result))
                {
                    result = default;
                }

                return true;
            }

            result = default;
            return false;
        }

        private static readonly ConcurrentDictionary<(Type dispatchResultType, Type resultType), Func<IDispatchResult, object>> _getResultInvokers
            = new ConcurrentDictionary<(Type dispatchResultType, Type resultType), Func<IDispatchResult, object>>();

        // Caches the delegate
        private static readonly Func<(Type dispatchResultType, Type resultType), Func<IDispatchResult, object>> _buildResultInvoker = BuildResultInvoker;

        private static bool TryGetResult<TResult>(IDispatchResult dispatchResult, out TResult result)
        {
            // This is the hot path when the desired result is the actual result or a base-type of it.
            if (dispatchResult is IDispatchResult<TResult> typedDispatchResult)
            {
                result = typedDispatchResult.Result;
                return true;
            }

            var resultInvoker = GetResultInvoker(dispatchResult.GetType(), typeof(TResult));

            if (resultInvoker != null)
            {
                result = (TResult)resultInvoker(dispatchResult);
                return true;
            }

            result = default;
            return false;
        }

        private static Func<IDispatchResult, object> GetResultInvoker(Type dispatchResultType, Type resultType)
        {
            return _getResultInvokers.GetOrAdd((dispatchResultType, resultType), _buildResultInvoker);
        }

        private static Func<IDispatchResult, object> BuildResultInvoker((Type dispatchResultType, Type resultType) args)
        {
            var interfaces = args.dispatchResultType.GetInterfaces();

            foreach (var @interface in interfaces)
            {
                if (!@interface.IsGenericType)
                    continue;

                if (@interface.GetGenericTypeDefinition() != typeof(IDispatchResult<>))
                    continue;

                var resultType1 = @interface.GetGenericArguments().First();

                if (!args.resultType.IsAssignableFrom(resultType1))
                    continue;

                var property = @interface.GetProperty("Result");
                Debug.Assert(property != null);

                var dispatchResultParameter = Expression.Parameter(typeof(IDispatchResult), "dispatchResult");
                var dispatchResultConvert = Expression.Convert(dispatchResultParameter, @interface);
                var resultAccess = Expression.MakeMemberAccess(dispatchResultConvert, property);
                var resultConvert = Expression.Convert(resultAccess, typeof(object));
                var lambda = Expression.Lambda<Func<IDispatchResult, object>>(resultConvert, dispatchResultParameter);
                return lambda.Compile();
            }

            return null;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a success result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="result">Contains the result value if the operation returns true.</param>
        /// <returns>True if <paramref name="dispatchResult"/> is a success result, false otherwise.</returns>
        public static bool IsSuccessWithResult(
            this IDispatchResult dispatchResult, out object result)
        {
            return dispatchResult.IsSuccessWithResult<object>(out result);
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a success result and the result value is of the specified or an assignable type.
        /// </summary>
        /// <typeparam name="TResult">The type of expected result value.</typeparam>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="result">Contains the result value if the operation returns true.</param>
        /// <returns>True if <paramref name="dispatchResult"/> is a success result, false otherwise.</returns>
        public static bool IsSuccessWithResult<TResult>(
            this IDispatchResult dispatchResult, out TResult result)
        {
            return IsSuccess(dispatchResult, out result) && !ReferenceEquals(result, null);
        }


        // TODO: Review this 
        internal static IEnumerable<TResult> GetResults<TResult>(
            this IDispatchResult dispatchResult, bool throwOnFailure)
        {
            if (dispatchResult.IsAggregateResult(out var aggregateDispatchResult))
            {
                var flattenedDispatchResult = aggregateDispatchResult.Flatten();
                return GetResultsFromFlattened<TResult>(flattenedDispatchResult, throwOnFailure);
            }

            if (dispatchResult.IsSuccessWithResult<TResult>(out var result))
            {
                return Enumerable.Repeat(result, count: 1);
            }

            if (throwOnFailure)
            {
                throw new FailureOrTypeMismatchException();
            }

            return Enumerable.Empty<TResult>();
        }

        private static IEnumerable<TResult> GetResultsFromFlattened<TResult>(
            IAggregateDispatchResult dispatchResult,
            bool throwOnFailure)
        {
            var results = new List<TResult>();

            foreach (var singleDispatchResult in dispatchResult.DispatchResults)
            {
                GetResultFromNonAggregate(singleDispatchResult, throwOnFailure, results);
            }

            return results;
        }

        private static void GetResultFromNonAggregate<TResult>(
            IDispatchResult dispatchResult,
            bool throwOnFailure,
            ICollection<TResult> results)
        {
            if (dispatchResult.IsSuccessWithResult<TResult>(out var singleResult))
            {
                results.Add(singleResult);
            }
            else if (throwOnFailure)
            {
                throw new FailureOrTypeMismatchException();
            }
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a dispatch failure result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <returns>True if <paramref name="dispatchResult"/> is a dispatch failure result, false otherwise.</returns>
        public static bool IsDispatchFailure(this IDispatchResult dispatchResult)
        {
            if (dispatchResult is IAggregateDispatchResult aggregateDispatchResult)
            {
                return aggregateDispatchResult
                    .Flatten()
                    .DispatchResults
                    .OfType<DispatchFailureDispatchResult>()
                    .Any();
            }

            return dispatchResult is DispatchFailureDispatchResult;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the specified dispatch result
        /// is a dispatch failure result.
        /// </summary>
        /// <param name="dispatchResult">The dispatch result.</param>
        /// <param name="messageType">
        /// Contains the message type of the message that could not be dispatched if the operation returns true.
        /// </param>
        /// <returns>True if <paramref name="dispatchResult"/> is a dispatch failure result, false otherwise.</returns>
        public static bool IsDispatchFailure(
            this IDispatchResult dispatchResult, out Type messageType)
        {
            if (dispatchResult is IAggregateDispatchResult aggregateDispatchResult)
            {
                var dispatchFailure1 = aggregateDispatchResult
                    .Flatten()
                    .DispatchResults
                    .OfType<DispatchFailureDispatchResult>()
                    .FirstOrDefault();

                if (dispatchFailure1 != null)
                {
                    messageType = dispatchFailure1.MessageType;
                    return true;
                }

                messageType = default;
                return false;
            }

            if (dispatchResult is DispatchFailureDispatchResult dispatchFailure)
            {
                messageType = dispatchFailure.MessageType;
                return true;
            }

            messageType = default;
            return false;
        }
    }
}
