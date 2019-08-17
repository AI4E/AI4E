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
    /// <summary>
    /// Represents the result of a projection.
    /// </summary>
    public sealed class ProjectionResult : IProjectionResult
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionResult"/> type.
        /// </summary>
        /// <param name="resultType">The type of projection result.</param>
        /// <param name="result">The projection result.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="resultType"/> or <paramref name="result"/> is <c>null</c>.
        /// </exception>
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
            ResultId = ResultIdType == null ? null : DataPropertyHelper.GetId(ResultType, Result);
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
}
