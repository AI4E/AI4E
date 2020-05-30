/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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

// We do not use a bool to indicate entity commit success to be able 
// to extend the range of reportable errors in the future.

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Defines constants indicating entity commit result status.
    /// </summary>
    public enum EntityCommitResult
    {
        /// <summary>
        /// The commit was successful.
        /// </summary>
        Success = 0,

        /// <summary>
        /// A concurrency failure occurred.
        /// </summary>
        ConcurrencyFailure = 1
    }
}
