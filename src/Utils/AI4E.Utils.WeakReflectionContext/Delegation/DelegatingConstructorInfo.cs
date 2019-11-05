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
    internal class DelegatingConstructorInfo : ConstructorInfo
    {
        private readonly WeakReference<ConstructorInfo> _underlyingConstructor;

        public DelegatingConstructorInfo(ConstructorInfo constructor)
        {
            Debug.Assert(null != constructor);

            _underlyingConstructor = new WeakReference<ConstructorInfo>(constructor);
        }

        public ConstructorInfo UnderlyingConstructor
        {
            get
            {
                if (_underlyingConstructor.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override MethodAttributes Attributes => UnderlyingConstructor.Attributes;

        public override CallingConventions CallingConvention => UnderlyingConstructor.CallingConvention;

        public override bool ContainsGenericParameters => UnderlyingConstructor.ContainsGenericParameters;

        public override Type? DeclaringType => UnderlyingConstructor.DeclaringType;

        public override bool IsGenericMethod => UnderlyingConstructor.IsGenericMethod;

        public override bool IsGenericMethodDefinition => UnderlyingConstructor.IsGenericMethodDefinition;

        public override bool IsSecurityCritical => UnderlyingConstructor.IsSecurityCritical;

        public override bool IsSecuritySafeCritical => UnderlyingConstructor.IsSecuritySafeCritical;

        public override bool IsSecurityTransparent => UnderlyingConstructor.IsSecurityTransparent;

        public override int MetadataToken => UnderlyingConstructor.MetadataToken;

        public override RuntimeMethodHandle MethodHandle => UnderlyingConstructor.MethodHandle;

        public override Module Module => UnderlyingConstructor.Module;

        public override string Name => UnderlyingConstructor.Name;

        public override Type? ReflectedType => UnderlyingConstructor.ReflectedType;

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return UnderlyingConstructor.GetCustomAttributes(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return UnderlyingConstructor.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return UnderlyingConstructor.GetCustomAttributesData();
        }

        public override Type[] GetGenericArguments()
        {
            return UnderlyingConstructor.GetGenericArguments();
        }

        public override MethodBody? GetMethodBody()
        {
            return UnderlyingConstructor.GetMethodBody();
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            return UnderlyingConstructor.GetMethodImplementationFlags();
        }

        public override ParameterInfo[] GetParameters()
        {
            return UnderlyingConstructor.GetParameters();
        }

        public override object Invoke(BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
        {
            return UnderlyingConstructor.Invoke(invokeAttr, binder, parameters, culture);
        }

        public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
        {
            return UnderlyingConstructor.Invoke(obj, invokeAttr, binder, parameters, culture);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return UnderlyingConstructor.IsDefined(attributeType, inherit);
        }

        public override string? ToString()
        {
            return UnderlyingConstructor.ToString();
        }
    }
}
