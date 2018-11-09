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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Routing;

namespace AI4E.Modularity
{
    public static class RunningModuleLookupExtension
    {
        // TODO: Do the two operations "GetPrefixesAsync" and "GetEndPointsAsync" have to be called atomically? 
        //       Does we have to and how can we ensure consistency?
        public static async Task<IEnumerable<EndPointAddress>> GetEndPointsAsync(this IRunningModuleLookup runningModules, ModuleIdentifier module, CancellationToken cancellation)
        {
            if (runningModules == null)
                throw new ArgumentNullException(nameof(runningModules));

            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            var prefixes = await runningModules.GetPrefixesAsync(module, cancellation);
            var result = await Task.WhenAll(prefixes.Select(prefix => runningModules.GetEndPointsAsync(prefix, cancellation).AsTask())); // TODO: Use ValueTaskEx.WhenAll
            var flattenedResult = result.SelectMany(p => p);

            return flattenedResult.Distinct();
        }

        public static async Task<EndPointAddress> MapHttpPathAsync(this IRunningModuleLookup runningModules, string path, CancellationToken cancellation)
        {
            if (runningModules == null)
                throw new ArgumentNullException(nameof(runningModules));

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullOrWhiteSpaceException(nameof(path));

            for (var a = new StringBuilder(path); a.Length > 0; a.Length = a.LastIndexOf('/', startIndex: 0, ignoreCase: true))
            {
                // TODO: Performance optimization: We are working with a string, turning it into a stringbuilder, than a string, a stringbuilder...
                var endPoints = await runningModules.GetEndPointsAsync(a.ToString(), cancellation);

                // We take the entry that was registered first. This is done in order that a logical end-point cannot override an address of an already existing end-point.
                var endPoint = endPoints.FirstOrDefault();

                if (endPoint != default)
                {
                    return endPoint;
                }
            }

            return EndPointAddress.UnknownAddress;
        }
    }
}