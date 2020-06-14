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
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AI4E.Utils.Memory.Compatibility
{
    public static class StringHelper
    {
        private static readonly CreateShim? _createShim = BuildCreateShim(typeof(string));

        private static CreateShim? BuildCreateShim(Type stringType)
        {
            var ctor = stringType.GetConstructor(BindingFlags.Instance | BindingFlags.Public,
                                                 Type.DefaultBinder,
                                                 new Type[] { typeof(ReadOnlySpan<char>) },
                                                 modifiers: null);

            if (ctor == null)
                return null;

            var valueParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "value");
            var call = Expression.New(ctor, valueParameter);
            var lambda = Expression.Lambda<CreateShim>(call, valueParameter);
            return lambda.Compile();
        }

        private delegate string CreateShim(ReadOnlySpan<char> value);

        public static string Create(ReadOnlySpan<char> value)
        {
            if (_createShim != null)
            {
                return _createShim(value);
            }

            var result = new string('\0', value.Length);
            var dest = MemoryMarshal.AsMemory(result.AsMemory()).Span;

            value.CopyTo(dest);

            return result;
        }
    }
}
