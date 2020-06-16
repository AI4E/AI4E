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
using System.Reflection;
using AI4E.Utils.Delegation;

namespace AI4E.Utils.Projection
{
    // Recursively 'projects' any assemblies, modules, types and members returned by a given method
    internal class ProjectingMethodInfo : DelegatingMethodInfo, IProjectable
    {
        public ProjectingMethodInfo(MethodInfo method, Projector projector)
            : base(method)
        {
            Debug.Assert(null != projector);

            Projector = projector!;
        }

        public Projector Projector { get; }

        public override Type? DeclaringType => Projector.ProjectType(base.DeclaringType);

        public override Module Module => Projector.ProjectModule(base.Module);

        public override Type? ReflectedType => Projector.ProjectType(base.ReflectedType);

        public override ParameterInfo ReturnParameter => Projector.ProjectParameter(base.ReturnParameter);

        public override ICustomAttributeProvider ReturnTypeCustomAttributes
        {
            get
            {
                // We should just return MethodInfo.ReturnParameter here
                // but DynamicMethod returns a fake ICustomAttributeProvider.
                var provider = base.ReturnTypeCustomAttributes;

                if (provider is ParameterInfo)
                    return Projector.ProjectParameter(ReturnParameter);

                return provider;
            }
        }

        public override Type ReturnType => Projector.ProjectType(base.ReturnType);

        public override MethodInfo GetBaseDefinition()
        {
            return Projector.ProjectMethod(base.GetBaseDefinition());
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            attributeType = Projector.Unproject(attributeType);

            return base.GetCustomAttributes(attributeType, inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return Projector.Project(base.GetCustomAttributesData(), Projector.ProjectCustomAttributeData!);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            attributeType = Projector.Unproject(attributeType);

            return base.IsDefined(attributeType, inherit);
        }

        public override Type[] GetGenericArguments()
        {
            return Projector.Project(base.GetGenericArguments(), Projector.ProjectType!);
        }

        public override MethodInfo GetGenericMethodDefinition()
        {
            return Projector.ProjectMethod(base.GetGenericMethodDefinition());
        }

        public override MethodBody? GetMethodBody()
        {
            return Projector.ProjectMethodBody(base.GetMethodBody());
        }

        public override ParameterInfo[] GetParameters()
        {
            return Projector.Project(base.GetParameters(), Projector.ProjectParameter!);
        }

        public override MethodInfo MakeGenericMethod(params Type[] typeArguments)
        {
            return Projector.ProjectMethod(base.MakeGenericMethod(Projector.Unproject(typeArguments)));
        }

        public override Delegate CreateDelegate(Type delegateType)
        {
            return base.CreateDelegate(Projector.Unproject(delegateType));
        }

        public override Delegate CreateDelegate(Type delegateType, object? target)
        {
            return base.CreateDelegate(Projector.Unproject(delegateType), target);
        }

        public override bool Equals(object? o)
        {
            return o is ProjectingMethodInfo other &&
                   Projector == other.Projector &&
                   UnderlyingMethod.Equals(other.UnderlyingMethod);
        }

        public override int GetHashCode()
        {
            return Projector.GetHashCode() ^ UnderlyingMethod.GetHashCode();
        }
    }
}
