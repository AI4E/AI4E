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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * AspNet Core (https://github.com/aspnet/AspNetCore)
 * Copyright (c) .NET Foundation. All rights reserved.
 * Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

namespace AI4E.AspNetCore.Components.Routing
{
    internal class RouteEntry
    {
        public RouteEntry(RouteTemplate template, Type handler, string[] unusedRouteParameterNames)
        {
            Template = template;
            UnusedRouteParameterNames = unusedRouteParameterNames;
            Handler = handler;
        }

        public RouteTemplate Template { get; }

        public string[] UnusedRouteParameterNames { get; }

        public Type Handler { get; }

        internal void Match(RouteContext context)
        {
            if (Template.Segments.Length != context.Segments.Length)
            {
                return;
            }

            // Parameters will be lazily initialized.
            Dictionary<string, object?>? parameters = null;
            for (var i = 0; i < Template.Segments.Length; i++)
            {
                var segment = Template.Segments[i];
                var pathSegment = context.Segments[i];
                if (!segment.Match(pathSegment, out var matchedParameterValue))
                {
                    return;
                }
                else
                {
                    if (segment.IsParameter)
                    {
                        parameters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
                        parameters[segment.Value] = matchedParameterValue;
                    }
                }
            }

            // In addition to extracting parameter values from the URL, each route entry
            // also knows which other parameters should be supplied with null values. These
            // are parameters supplied by other route entries matching the same handler.
            if (UnusedRouteParameterNames.Length > 0)
            {
                parameters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < UnusedRouteParameterNames.Length; i++)
                {
                    parameters[UnusedRouteParameterNames[i]] = null;
                }
            }

            context.Parameters = parameters;
            context.Handler = Handler;
        }
    }
}
