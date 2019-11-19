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

using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System;

namespace AI4E.Utils.Delegation
{
    // Recursively 'projects' any assemblies, modules, types and members returned by a given method
    internal class DelegatingMethodInfo : MethodInfo
    {
        private readonly WeakReference<MethodInfo> _underlyingMethod;

        public DelegatingMethodInfo(MethodInfo method)
        {
            Debug.Assert(null != method);

            _underlyingMethod = new WeakReference<MethodInfo>(method!);
        }

        public MethodInfo UnderlyingMethod
        {
            get
            {
                if (_underlyingMethod.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override MethodAttributes Attributes => UnderlyingMethod.Attributes;

        public override CallingConventions CallingConvention => UnderlyingMethod.CallingConvention;

        public override bool ContainsGenericParameters => UnderlyingMethod.ContainsGenericParameters;

        public override Type? DeclaringType => UnderlyingMethod.DeclaringType;

        public override bool IsGenericMethod => UnderlyingMethod.IsGenericMethod;

        public override bool IsGenericMethodDefinition => UnderlyingMethod.IsGenericMethodDefinition;

        public override bool IsSecurityCritical => UnderlyingMethod.IsSecurityCritical;

        public override bool IsSecuritySafeCritical => UnderlyingMethod.IsSecuritySafeCritical;

        public override bool IsSecurityTransparent => UnderlyingMethod.IsSecurityTransparent;

        public override int MetadataToken => UnderlyingMethod.MetadataToken;

        public override RuntimeMethodHandle MethodHandle => UnderlyingMethod.MethodHandle;

        public override Module Module => UnderlyingMethod.Module;

        public override string Name => UnderlyingMethod.Name;

        public override Type? ReflectedType => UnderlyingMethod.ReflectedType;

        public override ParameterInfo ReturnParameter => UnderlyingMethod.ReturnParameter;

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => UnderlyingMethod.ReturnTypeCustomAttributes;

        public override Type ReturnType => UnderlyingMethod.ReturnType;

        public override MethodInfo GetBaseDefinition()
        {
            return UnderlyingMethod.GetBaseDefinition();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return UnderlyingMethod.GetCustomAttributes(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return UnderlyingMethod.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return UnderlyingMethod.GetCustomAttributesData();
        }

        public override Type[] GetGenericArguments()
        {
            return UnderlyingMethod.GetGenericArguments();
        }

        public override MethodInfo GetGenericMethodDefinition()
        {
            return UnderlyingMethod.GetGenericMethodDefinition();
        }

        public override MethodBody? GetMethodBody()
        {
            return UnderlyingMethod.GetMethodBody();
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            return UnderlyingMethod.GetMethodImplementationFlags();
        }

        public override ParameterInfo[] GetParameters()
        {
            return UnderlyingMethod.GetParameters();
        }

        public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
        {
            return UnderlyingMethod.Invoke(obj, invokeAttr, binder, parameters, culture);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return UnderlyingMethod.IsDefined(attributeType, inherit);
        }

        public override MethodInfo MakeGenericMethod(params Type[] typeArguments)
        {
            return UnderlyingMethod.MakeGenericMethod(typeArguments);
        }

        public override Delegate CreateDelegate(Type delegateType)
        {
            return UnderlyingMethod.CreateDelegate(delegateType);
        }

        public override Delegate CreateDelegate(Type delegateType, object? target)
        {
            return UnderlyingMethod.CreateDelegate(delegateType, target);
        }

        public override string? ToString()
        {
            return UnderlyingMethod.ToString();
        }
    }
}
