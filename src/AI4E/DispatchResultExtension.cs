/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using System.Linq;
using AI4E.DispatchResults;

namespace AI4E
{
    public static partial class DispatchResultExtension
    {
        public static bool IsNotAuthorized(this IDispatchResult dispatchResult)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is NotAuthenticatedDispatchResult;
        }

        public static bool IsNotAuthenticated(this IDispatchResult dispatchResult)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is NotAuthenticatedDispatchResult;
        }

        public static bool IsValidationFailed(this IDispatchResult dispatchResult)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is ValidationFailureDispatchResult;
        }

        public static bool IsValidationFailed(this IDispatchResult dispatchResult, out IEnumerable<ValidationResult> validationResults)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
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
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is ConcurrencyIssueDispatchResult;
        }

        public static bool IsEntityNotFound(this IDispatchResult dispatchResult)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is EntityNotFoundDispatchResult;
        }

        public static bool IsEntityNotFound(this IDispatchResult dispatchResult, out Type entityType, out string id)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            if (dispatchResult is EntityNotFoundDispatchResult entityNotFoundDispatchResult)
            {
                entityType = entityNotFoundDispatchResult.EntityType;
                id = entityNotFoundDispatchResult.Id;
                return true;
            }

            entityType = default;
            id = default;

            return false;
        }

        public static bool IsEntityAlreadPresent(this IDispatchResult dispatchResult)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is EntityAlreadyPresentDispatchResult;
        }

        public static bool IsEntityAlreadPresent(this IDispatchResult dispatchResult, out Type entityType, out string id)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            if (dispatchResult is EntityAlreadyPresentDispatchResult entityAlreadyPresentDispatchResult)
            {
                entityType = entityAlreadyPresentDispatchResult.EntityType;
                id = entityAlreadyPresentDispatchResult.Id;
                return true;
            }

            entityType = default;
            id = default;

            return false;
        }

        public static bool IsTimeout(this IDispatchResult dispatchResult)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is TimeoutDispatchResult;
        }

        public static bool IsTimeout(this IDispatchResult dispatchResult, out DateTime dueTime)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
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
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is IAggregateDispatchResult;
        }

        public static bool IsAggregateResult(this IDispatchResult dispatchResult, out IAggregateDispatchResult aggregateDispatchResult)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

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

            var results = new List<IDispatchResult>();
            var resultsData = new List<IReadOnlyDictionary<string, object>>();
            FlattenInternal(aggregateDispatchResult, results, resultsData);

            if (resultsData.Any())
            {
                var resultDataBuilder = ImmutableDictionary.CreateBuilder<string, object>();
                var combinedResultDataEntries = new Dictionary<string, List<object>>();

                foreach (var resultData in resultsData)
                {
                    foreach (var kvp in resultData)
                    {
                        var key = kvp.Key;
                        var value = kvp.Value;

                        if (combinedResultDataEntries.TryGetValue(key, out var list))
                        {
                            list.Add(value);
                        }
                        else if (resultDataBuilder.TryGetValue(key, out var existingEntry))
                        {
                            list = new List<object> { value, existingEntry };

                            combinedResultDataEntries.Add(key, list);
                            resultDataBuilder[key] = list;
                        }
                        else
                        {
                            resultDataBuilder.Add(key, value);
                        }
                    }
                }

                if (resultDataBuilder.Any())
                {
                    return new AggregateDispatchResultDictionary(new AggregateDispatchResult(results), resultDataBuilder.ToImmutable());
                }
            }

            return new AggregateDispatchResult(results);
        }

        private static void FlattenInternal(IAggregateDispatchResult aggregateDispatchResult,
                                            List<IDispatchResult> results,
                                            List<IReadOnlyDictionary<string, object>> resultsData)
        {
            if (aggregateDispatchResult is AggregateDispatchResultDictionary aggregateResultData)
            {
                resultsData.Add(aggregateResultData);
            }

            foreach (var dispatchResult in aggregateDispatchResult.DispatchResults)
            {
                if (dispatchResult is IAggregateDispatchResult innerAggregateEventResult)
                {
                    FlattenInternal(innerAggregateEventResult, results, resultsData);
                }
                else if (dispatchResult is DispatchResultDictionary resultData)
                {
                    resultsData.Add(resultData);
                    results.Add(resultData.DispatchResult);
                }
                else
                {
                    results.Add(dispatchResult);
                }
            }
        }

        public static bool IsNotFound(this IDispatchResult dispatchResult)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult is NotFoundDispatchResult;
        }

        public static bool IsSuccess(this IDispatchResult dispatchResult, out object result)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult.IsSuccess<object>(out result);
        }

        public static bool IsSuccess<TResult>(this IDispatchResult dispatchResult, out TResult result)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
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
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            return dispatchResult.IsSuccessWithResult<object>(out result);
        }

        public static bool IsSuccessWithResult<TResult>(this IDispatchResult dispatchResult, out TResult result)
        {
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
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
            if (dispatchResult is DispatchResultDictionary resultData)
            {
                dispatchResult = resultData.DispatchResult;
            }

            if (dispatchResult.IsAggregateResult(out var aggregateDispatchResult))
            {
                aggregateDispatchResult = aggregateDispatchResult.Flatten();

                foreach (var singleDispatchResult in aggregateDispatchResult.DispatchResults)
                {
                    if (singleDispatchResult.IsSuccessWithResult<TResult>(out var singleResult))
                    {
                        yield return singleResult;
                    }
                    else if (throwOnFailure)
                    {
                        throw new FailureOrTypeMismatchException();
                    }
                }
            }
            else if (dispatchResult.IsSuccessWithResult<TResult>(out var result))
            {
                yield return result;
            }
            else if (throwOnFailure)
            {
                throw new FailureOrTypeMismatchException();
            }
        }
    }
}
