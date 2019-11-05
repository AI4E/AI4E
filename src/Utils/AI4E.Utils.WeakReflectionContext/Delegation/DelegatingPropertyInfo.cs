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
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace AI4E.Utils.Delegation
{
    internal class DelegatingPropertyInfo : PropertyInfo
    {
        private readonly WeakReference<PropertyInfo> _underlyingProperty;

        public DelegatingPropertyInfo(PropertyInfo property)
        {
            Debug.Assert(null != property);

            _underlyingProperty = new WeakReference<PropertyInfo>(property!);
        }

        public PropertyInfo UnderlyingProperty
        {
            get
            {
                if (_underlyingProperty.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override PropertyAttributes Attributes => UnderlyingProperty.Attributes;

        public override bool CanRead => UnderlyingProperty.CanRead;

        public override bool CanWrite => UnderlyingProperty.CanWrite;

        public override Type? DeclaringType => UnderlyingProperty.DeclaringType;

        public override int MetadataToken => UnderlyingProperty.MetadataToken;

        public override Module Module => UnderlyingProperty.Module;

        public override string Name => UnderlyingProperty.Name;

        public override Type PropertyType => UnderlyingProperty.PropertyType;

        public override Type? ReflectedType => UnderlyingProperty.ReflectedType;

        public override MethodInfo[] GetAccessors(bool nonPublic)
        {
            return UnderlyingProperty.GetAccessors(nonPublic);
        }

        public override MethodInfo? GetGetMethod(bool nonPublic)
        {
            return UnderlyingProperty.GetGetMethod(nonPublic);
        }

        public override ParameterInfo[] GetIndexParameters()
        {
            return UnderlyingProperty.GetIndexParameters();
        }

        public override MethodInfo? GetSetMethod(bool nonPublic)
        {
            return UnderlyingProperty.GetSetMethod(nonPublic);
        }

        public override object? GetValue(object? obj, object?[]? index)
        {
            return UnderlyingProperty.GetValue(obj, index);
        }

        public override object? GetValue(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture)
        {
            return UnderlyingProperty.GetValue(obj, invokeAttr, binder, index, culture);
        }

        public override void SetValue(object? obj, object? value, object?[]? index)
        {
            UnderlyingProperty.SetValue(obj, value, index);
        }

        public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture)
        {
            UnderlyingProperty.SetValue(obj, value, invokeAttr, binder, index, culture);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return UnderlyingProperty.GetCustomAttributes(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return UnderlyingProperty.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return UnderlyingProperty.GetCustomAttributesData();
        }

        public override object? GetConstantValue()
        {
            return UnderlyingProperty.GetConstantValue();
        }

        public override object? GetRawConstantValue()
        {
            return UnderlyingProperty.GetRawConstantValue();
        }

        public override Type[] GetOptionalCustomModifiers()
        {
            return UnderlyingProperty.GetOptionalCustomModifiers();
        }

        public override Type[] GetRequiredCustomModifiers()
        {
            return UnderlyingProperty.GetRequiredCustomModifiers();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return UnderlyingProperty.IsDefined(attributeType, inherit);
        }

        public override string? ToString()
        {
            return UnderlyingProperty.ToString();
        }
    }
}
