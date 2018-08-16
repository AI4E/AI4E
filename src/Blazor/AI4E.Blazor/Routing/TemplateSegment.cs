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

/* Based on
* --------------------------------------------------------------------------------------------------------------------
* Asp.Net Blazor
* Copyright (c) .NET Foundation. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use
* these files except in compliance with the License. You may obtain a copy of the
* License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed
* under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
* CONDITIONS OF ANY KIND, either express or implied. See the License for the
* specific language governing permissions and limitations under the License.
* --------------------------------------------------------------------------------------------------------------------
*/

using System;
using System.Linq;

namespace AI4E.Blazor.Routing
{
    internal class TemplateSegment
    {
        public TemplateSegment(string template, string segment, bool isParameter)
        {
            IsParameter = isParameter;

            if (!isParameter || segment.IndexOf(':') < 0)
            {
                Value = segment;
                Constraints = Array.Empty<RouteConstraint>();
            }
            else
            {
                var tokens = segment.Split(':');
                if (tokens[0].Length == 0)
                {
                    throw new ArgumentException($"Malformed parameter '{segment}' in route '{template}' has no name before the constraints list.");
                }

                Value = tokens[0];
                Constraints = tokens.Skip(1)
                    .Select(token => RouteConstraint.Parse(template, segment, token))
                    .ToArray();
            }
        }

        // The value of the segment. The exact text to match when is a literal.
        // The parameter name when its a segment
        public string Value { get; }

        public bool IsParameter { get; }

        public RouteConstraint[] Constraints { get; }

        public bool Match(string pathSegment, out object matchedParameterValue)
        {
            if (IsParameter)
            {
                matchedParameterValue = pathSegment;

                foreach (var constraint in Constraints)
                {
                    if (!constraint.Match(pathSegment, out matchedParameterValue))
                    {
                        return false;
                    }
                }

                return true;
            }
            else
            {
                matchedParameterValue = null;
                return string.Equals(Value, pathSegment, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
