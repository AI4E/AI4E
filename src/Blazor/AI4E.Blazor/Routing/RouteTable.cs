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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Blazor.Components;

namespace AI4E.Blazor.Routing
{
    internal class RouteTable
    {
        public RouteTable(RouteEntry[] routes)
        {
            Routes = routes;
        }

        public RouteEntry[] Routes { get; set; }

        public static RouteTable Create(IEnumerable<Type> types)
        {
            var routes = new List<RouteEntry>();
            foreach (var type in types)
            {
                var routeAttributes = type.GetCustomAttributes<RouteAttribute>(); // Inherit: true?
                foreach (var routeAttribute in routeAttributes)
                {
                    var template = TemplateParser.ParseTemplate(routeAttribute.Template);
                    var entry = new RouteEntry(template, type);
                    routes.Add(entry);
                }
            }

            return new RouteTable(routes.OrderBy(id => id, RoutePrecedence).ToArray());
        }

        public static IComparer<RouteEntry> RoutePrecedence { get; } = Comparer<RouteEntry>.Create(RouteComparison);

        /// <summary>
        /// Route precedence algorithm.
        /// We collect all the routes and sort them from most specific to
        /// less specific. The specificity of a route is given by the specificity
        /// of its segments and the position of those segments in the route.
        /// * A literal segment is more specific than a parameter segment.
        /// * A parameter segment with more constraints is more specific than one with fewer constraints
        /// * Segment earlier in the route are evaluated before segments later in the route.
        /// For example:
        /// /Literal is more specific than /Parameter
        /// /Route/With/{parameter} is more specific than /{multiple}/With/{parameters}
        /// /Product/{id:int} is more specific than /Product/{id}
        ///
        /// Routes can be ambiguous if:
        /// They are composed of literals and those literals have the same values (case insensitive)
        /// They are composed of a mix of literals and parameters, in the same relative order and the
        /// literals have the same values.
        /// For example:
        /// * /literal and /Literal
        /// /{parameter}/literal and /{something}/literal
        /// /{parameter:constraint}/literal and /{something:constraint}/literal
        ///
        /// To calculate the precedence we sort the list of routes as follows:
        /// * Shorter routes go first.
        /// * A literal wins over a parameter in precedence.
        /// * For literals with different values (case insensitive) we choose the lexical order
        /// * For parameters with different numbers of constraints, the one with more wins
        /// If we get to the end of the comparison routing we've detected an ambiguous pair of routes.
        /// </summary>
        internal static int RouteComparison(RouteEntry x, RouteEntry y)
        {
            var xTemplate = x.Template;
            var yTemplate = y.Template;
            if (xTemplate.Segments.Length != y.Template.Segments.Length)
            {
                return xTemplate.Segments.Length < y.Template.Segments.Length ? -1 : 1;
            }
            else
            {
                for (int i = 0; i < xTemplate.Segments.Length; i++)
                {
                    var xSegment = xTemplate.Segments[i];
                    var ySegment = yTemplate.Segments[i];
                    if (!xSegment.IsParameter && ySegment.IsParameter)
                    {
                        return -1;
                    }
                    if (xSegment.IsParameter && !ySegment.IsParameter)
                    {
                        return 1;
                    }

                    if (xSegment.IsParameter)
                    {
                        if (xSegment.Constraints.Length > ySegment.Constraints.Length)
                        {
                            return -1;
                        }
                        else if (xSegment.Constraints.Length < ySegment.Constraints.Length)
                        {
                            return 1;
                        }
                    }
                    else
                    {
                        var comparison = string.Compare(xSegment.Value, ySegment.Value, StringComparison.OrdinalIgnoreCase);
                        if (comparison != 0)
                        {
                            return comparison;
                        }
                    }
                }

                throw new InvalidOperationException($@"The following routes are ambiguous:
'{x.Template.TemplateText}' in '{x.Handler.FullName}'
'{y.Template.TemplateText}' in '{y.Handler.FullName}'
");
            }
        }

        internal void Route(RouteContext routeContext)
        {
            foreach (var route in Routes)
            {
                route.Match(routeContext);
                if (routeContext.Handler != null)
                {
                    return;
                }
            }
        }
    }
}
