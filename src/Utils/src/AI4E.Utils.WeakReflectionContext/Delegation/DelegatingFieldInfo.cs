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
    internal class DelegatingFieldInfo : FieldInfo
    {
        private readonly WeakReference<FieldInfo> _underlyingField;

        public DelegatingFieldInfo(FieldInfo field)
        {
            Debug.Assert(null != field);

            _underlyingField = new WeakReference<FieldInfo>(field!);
        }

        public FieldInfo UnderlyingField
        {
            get
            {
                if (_underlyingField.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override FieldAttributes Attributes => UnderlyingField.Attributes;

        public override Type? DeclaringType => UnderlyingField.DeclaringType;

        public override RuntimeFieldHandle FieldHandle => UnderlyingField.FieldHandle;

        public override Type FieldType => UnderlyingField.FieldType;

        public override bool IsSecurityCritical => UnderlyingField.IsSecurityCritical;

        public override bool IsSecuritySafeCritical => UnderlyingField.IsSecuritySafeCritical;

        public override bool IsSecurityTransparent => UnderlyingField.IsSecurityTransparent;

        public override int MetadataToken => UnderlyingField.MetadataToken;

        public override Module Module => UnderlyingField.Module;

        public override string Name => UnderlyingField.Name;

        public override Type? ReflectedType => UnderlyingField.ReflectedType;

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return UnderlyingField.GetCustomAttributes(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return UnderlyingField.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return UnderlyingField.GetCustomAttributesData();
        }

        public override Type[] GetOptionalCustomModifiers()
        {
            return UnderlyingField.GetOptionalCustomModifiers();
        }

        public override object? GetRawConstantValue()
        {
            return UnderlyingField.GetRawConstantValue();
        }

        public override Type[] GetRequiredCustomModifiers()
        {
            return UnderlyingField.GetRequiredCustomModifiers();
        }

        public override object? GetValue(object? obj)
        {
            return UnderlyingField.GetValue(obj);
        }

        public override object? GetValueDirect(TypedReference obj)
        {
            return UnderlyingField.GetValueDirect(obj);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return UnderlyingField.IsDefined(attributeType, inherit);
        }

        public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, CultureInfo? culture)
        {
            UnderlyingField.SetValue(obj, value, invokeAttr, binder, culture);
        }

        public override void SetValueDirect(TypedReference obj, object value)
        {
            UnderlyingField.SetValueDirect(obj, value);
        }

        public override string? ToString()
        {
            return UnderlyingField.ToString();
        }
    }
}
