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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;

namespace AI4E.Modularity.Host
{
    public sealed class PathMapper : IPathMapper
    {
        private readonly IModuleManager _moduleManager;

        public PathMapper(IModuleManager moduleManager)
        {
            if (moduleManager == null)
                throw new ArgumentNullException(nameof(moduleManager));

            _moduleManager = moduleManager;
        }

        public async ValueTask<RouteEndPointAddress> MapHttpPathAsync(ReadOnlyMemory<char> path, CancellationToken cancellation = default)
        {
            if (path.Span.IsEmptyOrWhiteSpace())
                throw new ArgumentException("The argument must not be an empty nor consist of whitespace only.", nameof(path));

            for (; !path.IsEmpty; path = SliceToNextSegment(path))
            {
                var endPoints = await _moduleManager.GetEndPointsAsync(path, cancellation);

                // We take the entry that was registered first.
                // This is done in order that a logical end-point cannot override an address of an already existing end-point.
                var endPoint = endPoints.FirstOrDefault();

                if (endPoint != default)
                {
                    return endPoint;
                }
            }

            return RouteEndPointAddress.UnknownAddress;
        }

        private static ReadOnlyMemory<char> SliceToNextSegment(ReadOnlyMemory<char> path)
        {
            var index = path.Span.LastIndexOf('/');

            if (index < 0)
                return ReadOnlyMemory<char>.Empty;

            return path.Slice(start: 0, length: index);
        }
    }
}
