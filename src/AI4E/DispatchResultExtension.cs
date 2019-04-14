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
using System.Collections.Generic;
using System.Linq;
using AI4E.DispatchResults;

namespace AI4E
{
    public static partial class DispatchResultExtension
    {
        public static bool IsNotAuthorized(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is NotAuthorizedDispatchResult);
            }

            return dispatchResult is NotAuthorizedDispatchResult;
        }

        public static bool IsNotAuthenticated(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is NotAuthenticatedDispatchResult);
            }

            return dispatchResult is NotAuthenticatedDispatchResult;
        }

        public static bool IsValidationFailed(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is ValidationFailureDispatchResult);
            }

            return dispatchResult is ValidationFailureDispatchResult;
        }

        public static bool IsValidationFailed(this IDispatchResult dispatchResult, out IEnumerable<ValidationResult> validationResults)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                var innerResults = aggregateDispatchResult.Flatten().DispatchResults.OfType<ValidationFailureDispatchResult>();

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

        public static bool IsConcurrencyIssue(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is ConcurrencyIssueDispatchResult);
            }

            return dispatchResult is ConcurrencyIssueDispatchResult;
        }

        public static bool IsEntityNotFound(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is EntityNotFoundDispatchResult);
            }

            return dispatchResult is EntityNotFoundDispatchResult;
        }

        public static bool IsEntityNotFound(this IDispatchResult dispatchResult, out string entityType, out string id)
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
                entityType = entityNotFoundDispatchResult.EntityTypeName;
                id = entityNotFoundDispatchResult.Id;
                return true;
            }

            entityType = default;
            id = default;

            return false;
        }

        public static bool IsEntityAlreadPresent(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is EntityAlreadyPresentDispatchResult);
            }

            return dispatchResult is EntityAlreadyPresentDispatchResult;
        }

        public static bool IsEntityAlreadPresent(this IDispatchResult dispatchResult, out Type entityType, out string id)
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

        public static bool IsTimeout(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is TimeoutDispatchResult);
            }

            return dispatchResult is TimeoutDispatchResult;
        }

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

        public static bool IsAggregateResult(this IDispatchResult dispatchResult)
        {
            return dispatchResult is IAggregateDispatchResult;
        }

        public static bool IsAggregateResult(this IDispatchResult dispatchResult, out IAggregateDispatchResult aggregateDispatchResult)
        {
            if (dispatchResult is IAggregateDispatchResult aggregateDispatchResult2)
            {
                aggregateDispatchResult = aggregateDispatchResult2;
                return true;
            }

            aggregateDispatchResult = default;
            return false;
        }

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

        private static void FlattenInternal(IAggregateDispatchResult aggregateDispatchResult, List<IDispatchResult> dispatchResults, out bool needsFlattening)
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

        public static bool IsNotFound(this IDispatchResult dispatchResult)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                return aggregateDispatchResult.Flatten().DispatchResults.Any(p => p is NotFoundDispatchResult);
            }

            return dispatchResult is NotFoundDispatchResult;
        }

        public static bool IsSuccess(this IDispatchResult dispatchResult, out object result)
        {
            return dispatchResult.IsSuccess<object>(out result);
        }

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
                    var typedDispatchResult = dispatchResults.OfType<IDispatchResult<TResult>>().FirstOrDefault();

                    if (typedDispatchResult != null)
                    {
                        result = typedDispatchResult.Result;
                    }

                    return true;
                }

                return false;
            }

            if (dispatchResult.IsSuccess)
            {
                if (dispatchResult is IDispatchResult<TResult> typedDispatchResult)
                {
                    result = typedDispatchResult.Result;
                }
                else
                {
                    result = default;
                }

                return true;
            }

            result = default;
            return false;
        }

        public static bool IsSuccessWithResult(this IDispatchResult dispatchResult, out object result)
        {
            return dispatchResult.IsSuccessWithResult<object>(out result);
        }

        public static bool IsSuccessWithResult<TResult>(this IDispatchResult dispatchResult, out TResult result)
        {
            if (IsAggregateResult(dispatchResult, out var aggregateDispatchResult))
            {
                dispatchResult = aggregateDispatchResult.Flatten()
                                                        .DispatchResults
                                                        .OfType<IDispatchResult<TResult>>()
                                                        .FirstOrDefault();
            }

            if (dispatchResult.IsSuccess && dispatchResult is IDispatchResult<TResult> typedDispatchResult)
            {
                result = typedDispatchResult.Result;
                return true;
            }

            result = default;
            return false;
        }

        public static IEnumerable<TResult> GetResults<TResult>(this IDispatchResult dispatchResult, bool throwOnFailure)
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

        private static IEnumerable<TResult> GetResultsFromFlattened<TResult>(IAggregateDispatchResult dispatchResult,
                                                                             bool throwOnFailure)
        {
            var results = new List<TResult>();

            foreach (var singleDispatchResult in dispatchResult.DispatchResults)
            {
                GetResultFromNonAggregate(singleDispatchResult, throwOnFailure, results);
            }

            return results;
        }

        private static void GetResultFromNonAggregate<TResult>(IDispatchResult dispatchResult,
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

        public static bool IsDispatchFailure(this IDispatchResult dispatchResult)
        {
            return dispatchResult is DispatchFailureDispatchResult;
        }

        public static bool IsDispatchFailure(this IDispatchResult dispatchResult, out Type messageType)
        {
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
