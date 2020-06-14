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

namespace AI4E.AspNetCore.Components.Routing
{
    /// <summary>
    /// A route constraint that requires the value to be parseable as a specified type.
    /// </summary>
    /// <typeparam name="T">The type to which the value must be parseable.</typeparam>
    internal class TypeRouteConstraint<T> : RouteConstraint
    {
        public delegate bool TryParseDelegate(string str, out T result);

        private readonly TryParseDelegate _parser;

        public TypeRouteConstraint(TryParseDelegate parser)
        {
            _parser = parser;
        }

        public override bool Match(string pathSegment, out object? convertedValue)
        {
            if (_parser(pathSegment, out var result))
            {
                convertedValue = result;
                return true;
            }
            else
            {
                convertedValue = null;
                return false;
            }
        }
    }
}
