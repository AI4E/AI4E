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
 * Fast Deep Copy by Expression Trees 
 * https://www.codeproject.com/articles/1111658/fast-deep-copy-by-expression-trees-c-sharp
 * 
 * MIT License
 * 
 * Copyright (c) 2014 - 2016 Frantisek Konopecky
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AI4E.Utils
{
    public static class ObjectInspector
    {
        #region Fields

        // This is a conditional weak table to allow assembly unloading.
        private static readonly ConditionalWeakTable<Type, CacheEntry> _isStructTypeToDeepCopy
            = new ConditionalWeakTable<Type, CacheEntry>();

        // We cache the delegates for perf reasons.
#pragma warning disable HAA0603
        private static readonly ConditionalWeakTable<Type, CacheEntry>.CreateValueCallback _isStructTypeToDeepCopyFactory = IsStructWhichNeedsDeepCopyFactory;
        private static readonly Func<Type, bool> _isClassOtherThanString = TypeExtension.IsClassOtherThanString;
#pragma warning restore HAA0603

        #endregion

        public static FieldInfo[] GetFieldsToCopy(this Type type)
        {
            if (type is null)
                throw new NullReferenceException();

            return GetAllRelevantFields(type, forceAllFields: false);
        }

        public static bool IsTypeToDeepCopy(this Type type)
        {
#pragma warning disable CA1062
            return type.IsClassOtherThanString() || IsStructWhichNeedsDeepCopy(type);
#pragma warning restore CA1062
        }

        private static FieldInfo[] GetAllRelevantFields(Type type, bool forceAllFields)
        {
            var fieldsList = new List<FieldInfo>();

            for (var typeCache = type; typeCache != null; typeCache = typeCache.BaseType!)
            {
                var fields = typeCache.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy).AsEnumerable();

                if (!forceAllFields)
                {
                    fields = fields.Where(field => IsTypeToDeepCopy(field.FieldType));
                }

                fieldsList.AddRange(fields);
            }

            return fieldsList.ToArray();
        }

        private static FieldInfo[] GetAllFields(Type type)
        {
            return GetAllRelevantFields(type, forceAllFields: true);
        }

        private static bool IsStructWhichNeedsDeepCopy(Type type)
        {
            // The following structure ensures that multiple threads can use the dictionary
            // even while dictionary is locked and being updated by other thread.
            // That is why we do not modify the old dictionary instance but
            // we replace it with a new instance everytime.

            return _isStructTypeToDeepCopy.GetValue(type, _isStructTypeToDeepCopyFactory)
                .IsTypeToDeepCopy;
        }

        private static CacheEntry IsStructWhichNeedsDeepCopyFactory(Type type)
        {
            return new CacheEntry(IsStructOtherThanBasicValueTypes(type) && HasInItsHierarchyFieldsWithClasses(type));
        }

        private static bool IsStructOtherThanBasicValueTypes(Type type)
        {
            return type.IsValueType &&
                  !type.IsPrimitive &&
                  !type.IsEnum &&
                   type != typeof(decimal);
        }

        private static bool HasInItsHierarchyFieldsWithClasses(Type type, HashSet<Type>? alreadyCheckedTypes = null)
        {
            alreadyCheckedTypes ??= new HashSet<Type>();
            alreadyCheckedTypes.Add(type);

            var allFields = GetAllFields(type);
            var allFieldTypes = allFields.Select(f => f.FieldType).Distinct().ToList();
            var hasFieldsWithClasses = allFieldTypes.Any(_isClassOtherThanString);

            if (hasFieldsWithClasses)
            {
                return true;
            }

            foreach (var typeToCheck in allFieldTypes)
            {
                if (!IsStructOtherThanBasicValueTypes(typeToCheck) ||
                   alreadyCheckedTypes.Contains(typeToCheck))
                {
                    continue;
                }

                if (HasInItsHierarchyFieldsWithClasses(typeToCheck, alreadyCheckedTypes))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class CacheEntry
        {
            public CacheEntry(bool isTypeToDeepCopy)
            {
                IsTypeToDeepCopy = isTypeToDeepCopy;
            }

            public bool IsTypeToDeepCopy { get; }
        }
    }
}
