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
 * corefx (https://github.com/dotnet/corefx)
 * Licensed to the .NET Foundation under one or more agreements.
 * The .NET Foundation licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

namespace AI4E.Utils
{
    internal static class CollectionServices
    {
        public static T[] Empty<T>()
        {
            return Array.Empty<T>();
        }

        public static bool CompareArrays<T>(T[] left, T[] right)
        {
            if (left.Length != right.Length)
                return false;

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] is null)
                {
                    return right is null;
                }

                if (right[i] is null)
                    return false;

                if (!left[i]!.Equals(right[i]))
                    return false;
            }

            return true;
        }

        public static int GetArrayHashCode<T>(T[] array)
        {
            int hashcode = 0;
            foreach (T t in array)
            {
                hashcode ^= t?.GetHashCode() ?? 0;
            }

            return hashcode;
        }

        public static object[] ConvertListToArray(List<object> list, Type arrayType)
        {
            // Mimic the behavior of GetCustomAttributes in runtime reflection.
            if (arrayType.HasElementType || arrayType.IsValueType || arrayType.ContainsGenericParameters)
                return list.ToArray();

            // Converts results to typed array.
            Array typedArray = Array.CreateInstance(arrayType, list.Count);

            list.CopyTo((object[])typedArray);

            return (object[])typedArray;
        }

        public static object[] IEnumerableToArray(IEnumerable<object> enumerable, Type arrayType)
        {
            List<object> list = new List<object>(enumerable);

            return ConvertListToArray(list, arrayType);
        }
    }
}
