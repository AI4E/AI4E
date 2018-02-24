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
using System.Linq;
using AI4E.DispatchResults;

namespace AI4E
{
    public static class DispatchResultExtension
    {
        public static bool IsNotAuthorized(this IDispatchResult DispatchResult)
        {
            return DispatchResult is NotAuthenticatedDispatchResult;
        }

        public static bool IsNotAuthenticated(this IDispatchResult DispatchResult)
        {
            return DispatchResult is NotAuthenticatedDispatchResult;
        }

        public static bool IsValidationFailed(this IDispatchResult DispatchResult)
        {
            return DispatchResult is ValidationFailureDispatchResult;
        }

        public static bool IsValidationFailed(this IDispatchResult DispatchResult, out IEnumerable<ValidationResult> validationResults)
        {
            if (DispatchResult is ValidationFailureDispatchResult validationFailureDispatchResult)
            {
                validationResults = validationFailureDispatchResult.ValidationResults;
                return true;
            }

            validationResults = Enumerable.Empty<ValidationResult>();
            return false;
        }

        public static bool IsConcurrencyIssue(this IDispatchResult DispatchResult)
        {
            return DispatchResult is ConcurrencyIssueDispatchResult;
        }

        public static bool IsEntityNotFound(this IDispatchResult DispatchResult)
        {
            return DispatchResult is EntityNotFoundDispatchResult;
        }

        public static bool IsEntityNotFound(this IDispatchResult DispatchResult, out Type entityType, out string id)
        {
            if (DispatchResult is EntityNotFoundDispatchResult entityNotFoundDispatchResult)
            {
                entityType = entityNotFoundDispatchResult.EntityType;
                id = entityNotFoundDispatchResult.Id;
                return true;
            }

            entityType = default;
            id = default;

            return false;
        }

        public static bool IsEntityAlreadPresent(this IDispatchResult DispatchResult)
        {
            return DispatchResult is EntityAlreadyPresentDispatchResult;
        }

        public static bool IsEntityAlreadPresent(this IDispatchResult DispatchResult, out Type entityType, out string id)
        {
            if (DispatchResult is EntityAlreadyPresentDispatchResult entityAlreadyPresentDispatchResult)
            {
                entityType = entityAlreadyPresentDispatchResult.EntityType;
                id = entityAlreadyPresentDispatchResult.Id;
                return true;
            }

            entityType = default;
            id = default;

            return false;
        }

        public static bool IsTimeout(this IDispatchResult DispatchResult)
        {
            return DispatchResult is TimeoutDispatchResult;
        }

        public static bool IsTimeout(this IDispatchResult DispatchResult, out DateTime dueTime)
        {
            if (DispatchResult is TimeoutDispatchResult timeoutDispatchResult)
            {
                dueTime = timeoutDispatchResult.DueTime;
                return true;
            }

            dueTime = default;
            return false;
        }


        public static bool IsAggregateResult(this IDispatchResult eventResult)
        {
            return eventResult is IAggregateDispatchResult;
        }

        public static bool IsAggregateResult(this IDispatchResult eventResult, out IAggregateDispatchResult aggregateDispatchResult)
        {
            if (eventResult is IAggregateDispatchResult aggregateDispatchResult2)
            {
                aggregateDispatchResult = aggregateDispatchResult2;
                return true;
            }

            aggregateDispatchResult = default;
            return false;
        }

        public static IAggregateDispatchResult Flatten(this IAggregateDispatchResult aggregateEventResult)
        {
            var result = new List<IDispatchResult>();
            AddEventResultsToList(aggregateEventResult, result);

            return new AggregateDispatchResult(result);
        }

        private static void AddEventResultsToList(IAggregateDispatchResult aggregateEventResult, List<IDispatchResult> list)
        {
            foreach (var eventResult in aggregateEventResult.DispatchResults)
            {
                if (eventResult is IAggregateDispatchResult innerAggregateEventResult)
                {
                    AddEventResultsToList(innerAggregateEventResult, list);
                }
                else
                {
                    list.Add(eventResult);
                }
            }
        }

        public static bool IsNotFound(this IDispatchResult queryResult)
        {
            return queryResult is NotFoundDispatchResult;
        }

        public static bool IsSuccess(this IDispatchResult dispatchResult, out object result)
        {
            return dispatchResult.IsSuccess<object>(out result);
        }

        public static bool IsSuccess<TResult>(this IDispatchResult dispatchResult, out TResult result)
            where TResult : class
        {
            if (dispatchResult.IsSuccess)
            {
                result = (dispatchResult as IDispatchResult<TResult>)?.Result;
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
            where TResult : class
        {
            if (dispatchResult.IsSuccess && dispatchResult is IDispatchResult<TResult> typedDispatchResult)
            {
                result = typedDispatchResult.Result;
                return true;
            }

            result = default;
            return false;
        }
    }
}
