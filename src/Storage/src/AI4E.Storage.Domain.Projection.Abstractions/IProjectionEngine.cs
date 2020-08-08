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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a projection engine.
    /// </summary>
    public interface IProjectionEngine
    {
        /// <summary>
        /// Projects the specified projection source.
        /// </summary>
        /// <param name="sourceType">The type of projection source.</param>
        /// <param name="sourceId">Type projection source id.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="sourceType"/> or <paramref name="sourceId"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="sourceType"/> specifies an invalid source type.
        /// </exception>
        Task ProjectAsync(
            Type sourceType,
            string sourceId,
            CancellationToken cancellation = default);
    }
}
