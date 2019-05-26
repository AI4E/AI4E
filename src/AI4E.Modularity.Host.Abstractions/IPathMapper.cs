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
using AI4E.Routing;

namespace AI4E.Modularity.Host
{
    /// <summary>
    /// Represents a mapper that maps uri paths to end-points.
    /// </summary>
    public interface IPathMapper
    {
        /// <summary>
        /// Asynchronously maps the specified uri path.
        /// </summary>
        /// <param name="path">The path to map.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the mapped end-point or <see cref="EndPointAddress.UnknownAddress"/> if no end-point matches.
        /// </returns>
        ValueTask<EndPointAddress> MapHttpPathAsync(ReadOnlyMemory<char> path, CancellationToken cancellation = default);
    }
}
