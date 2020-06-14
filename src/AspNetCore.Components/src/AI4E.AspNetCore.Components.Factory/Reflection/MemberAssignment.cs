/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Linq;
using System.Reflection;

#nullable disable

namespace AI4E.AspNetCore.Components.Factory.Reflection
{
    internal class MemberAssignment
    {
        public static IEnumerable<PropertyInfo> GetPropertiesIncludingInherited(
            Type type, BindingFlags bindingFlags)
        {
            var dictionary = new Dictionary<string, List<PropertyInfo>>();

            while (type != null)
            {
                var properties = type.GetProperties(bindingFlags)
                    .Where(prop => prop.DeclaringType == type);
                foreach (var property in properties)
                {
                    if (!dictionary.TryGetValue(property.Name, out var others))
                    {
                        others = new List<PropertyInfo>();
                        dictionary.Add(property.Name, others);
                    }

                    if (others.Any(other => other.GetMethod?.GetBaseDefinition() == property.GetMethod?.GetBaseDefinition()))
                    {
                        // This is an inheritance case. We can safely ignore the value of property since
                        // we have seen a more derived value.
                        continue;
                    }

                    others.Add(property);
                }

                type = type.BaseType;
            }

            return dictionary.Values.SelectMany(p => p);
        }

        public static IPropertySetter CreatePropertySetter(Type targetType, PropertyInfo property, bool cascading)
        {
            if (property.SetMethod == null)
            {
                throw new InvalidOperationException($"Cannot provide a value for property " +
                    $"'{property.Name}' on type '{targetType.FullName}' because the property " +
                    $"has no setter.");
            }

            return (IPropertySetter)Activator.CreateInstance(
                typeof(PropertySetter<,>).MakeGenericType(targetType, property.PropertyType),
                property.SetMethod,
                cascading);
        }

        class PropertySetter<TTarget, TValue> : IPropertySetter
        {
            private readonly Action<TTarget, TValue> _setterDelegate;

            public PropertySetter(MethodInfo setMethod, bool cascading)
            {
                _setterDelegate = (Action<TTarget, TValue>)Delegate.CreateDelegate(
                    typeof(Action<TTarget, TValue>), setMethod);
                Cascading = cascading;
            }

            public bool Cascading { get; }

            public void SetValue(object target, object value)
            {
                if (value == null)
                {
                    _setterDelegate((TTarget)target, default);
                }
                else
                {
                    _setterDelegate((TTarget)target, (TValue)value);
                }
            }
        }
    }
}
