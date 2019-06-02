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
using AI4E.Internal;

namespace AI4E.Storage.Projection
{
    public sealed class ProjectionResult : IProjectionResult
    {
        public ProjectionResult(Type resultType, object result)
        {
            if (resultType is null)
                throw new ArgumentNullException(nameof(resultType));

            if (result is null)
                throw new ArgumentNullException(nameof(result));

            // TODO: Check whether result is assignable to result-type

            ResultType = resultType;
            Result = result;

            ResultIdType = DataPropertyHelper.GetIdType(ResultType);
            ResultId = DataPropertyHelper.GetId(ResultType, Result);

            // TODO: Check whether an id can be accessed.
        }

        /// <inheritdoc />
        public object ResultId { get; }

        /// <inheritdoc />
        public object Result { get; }

        /// <inheritdoc />
        public Type ResultIdType { get; }

        /// <inheritdoc />
        public Type ResultType { get; }
    }

    public sealed class ProjectionResult<TResultId, TResult> : IProjectionResult<TResultId, TResult>
             where TResult : class
    {
        public ProjectionResult(TResult result)
        {
            if (result is null)
                throw new ArgumentNullException(nameof(result));

            Result = result;
            ResultId = DataPropertyHelper.GetId<TResultId, TResult>(result);
        }

        /// <inheritdoc />
        public TResultId ResultId { get; }

        /// <inheritdoc />
        public TResult Result { get; }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        object IProjectionResult.ResultId => ResultId;
        object IProjectionResult.Result => Result;
        Type IProjectionResult.ResultIdType => typeof(TResultId);
        Type IProjectionResult.ResultType => typeof(TResult);
#endif
    }
}
