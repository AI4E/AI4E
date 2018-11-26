/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IDispatchResult.cs 
 * Types:           (1) AI4E.IDispatchResult
 *                  (2) AI4E.IAggregateDispatchResult
 *                  (3) AI4E.IDispatchResult'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   15.07.2017 
 * Status:          Ready
 * --------------------------------------------------------------------------------------------------------------------
 */

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

using System.Collections.Generic;

namespace AI4E
{
    /// <summary>
    /// Represents the result of a message dispatch process.
    /// </summary>
    public interface IDispatchResult
    {
        /// <summary>
        /// Gets a boolean value indicating whether the dispatch and execution was successful.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Gets a description of the dispatch result.
        /// </summary>
        string Message { get; }

        IReadOnlyDictionary<string, object> ResultData { get; }
    }

    /// <summary>
    /// Aggregates multiple message dispatch results to a single result.
    /// </summary>
    public interface IAggregateDispatchResult : IDispatchResult
    {
        /// <summary>
        /// Gets the collection of aggregated dispatch results.
        /// </summary>
        IEnumerable<IDispatchResult> DispatchResults { get; }
    }

    /// <summary>
    /// Represents the result of a message dispatch process with a result value.
    /// </summary>
    /// <typeparam name="TResult">The type of result.</typeparam>
    public interface IDispatchResult<TResult> : IDispatchResult
    {
        /// <summary>
        /// Gets the result.
        /// </summary>
        TResult Result { get; }
    }
}
