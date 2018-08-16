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
using System.Globalization;

namespace AI4E.Blazor.Routing
{
    internal abstract class RouteConstraint
    {
        private static readonly IDictionary<string, RouteConstraint> _cachedConstraints
            = new Dictionary<string, RouteConstraint>();

        public abstract bool Match(string pathSegment, out object convertedValue);

        public static RouteConstraint Parse(string template, string segment, string constraint)
        {
            if (string.IsNullOrEmpty(constraint))
            {
                throw new ArgumentException($"Malformed segment '{segment}' in route '{template}' contains an empty constraint.");
            }

            if (_cachedConstraints.TryGetValue(constraint, out var cachedInstance))
            {
                return cachedInstance;
            }
            else
            {
                var newInstance = CreateRouteConstraint(constraint);
                if (newInstance != null)
                {
                    _cachedConstraints[constraint] = newInstance;
                    return newInstance;
                }
                else
                {
                    throw new ArgumentException($"Unsupported constraint '{constraint}' in route '{template}'.");
                }
            }
        }

        private static RouteConstraint CreateRouteConstraint(string constraint)
        {
            switch (constraint)
            {
                case "bool":
                    return new TypeRouteConstraint<bool>(bool.TryParse);
                case "datetime":
                    return new TypeRouteConstraint<DateTime>((string str, out DateTime result)
                        => DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out result));
                case "decimal":
                    return new TypeRouteConstraint<decimal>((string str, out decimal result)
                        => decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out result));
                case "double":
                    return new TypeRouteConstraint<double>((string str, out double result)
                        => double.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out result));
                case "float":
                    return new TypeRouteConstraint<float>((string str, out float result)
                        => float.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out result));
                case "guid":
                    return new TypeRouteConstraint<Guid>(Guid.TryParse);
                case "int":
                    return new TypeRouteConstraint<int>((string str, out int result)
                        => int.TryParse(str, NumberStyles.None, CultureInfo.InvariantCulture, out result));
                case "long":
                    return new TypeRouteConstraint<long>((string str, out long result)
                        => long.TryParse(str, NumberStyles.None, CultureInfo.InvariantCulture, out result));
                default:
                    return null;
            }
        }
    }
}
