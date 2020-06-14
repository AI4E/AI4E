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
using System.Globalization;

namespace AI4E.AspNetCore.Components.Routing
{
    internal abstract class RouteConstraint
    {
        private static readonly IDictionary<string, RouteConstraint> CachedConstraints
            = new Dictionary<string, RouteConstraint>();

        public abstract bool Match(string pathSegment, out object? convertedValue);

        public static RouteConstraint Parse(string template, string segment, string constraint)
        {
            if (string.IsNullOrEmpty(constraint))
            {
                throw new ArgumentException($"Malformed segment '{segment}' in route '{template}' contains an empty constraint.");
            }

            if (CachedConstraints.TryGetValue(constraint, out var cachedInstance))
            {
                return cachedInstance;
            }
            else
            {
                var newInstance = CreateRouteConstraint(constraint);
                if (newInstance != null)
                {
                    CachedConstraints[constraint] = newInstance;
                    return newInstance;
                }
                else
                {
                    throw new ArgumentException($"Unsupported constraint '{constraint}' in route '{template}'.");
                }
            }
        }

        private static RouteConstraint? CreateRouteConstraint(string constraint)
        {
            return constraint switch
            {
                "bool" => new TypeRouteConstraint<bool>(bool.TryParse),
                "datetime" => new TypeRouteConstraint<DateTime>((string str, out DateTime result)
                          => DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)),
                "decimal" => new TypeRouteConstraint<decimal>((string str, out decimal result)
                          => decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out result)),
                "double" => new TypeRouteConstraint<double>((string str, out double result)
                          => double.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out result)),
                "float" => new TypeRouteConstraint<float>((string str, out float result)
                          => float.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out result)),
                "guid" => new TypeRouteConstraint<Guid>(Guid.TryParse),
                "int" => new TypeRouteConstraint<int>((string str, out int result)
                          => int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)),
                "long" => new TypeRouteConstraint<long>((string str, out long result)
                          => long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)),
                _ => null,
            };
        }
    }
}
