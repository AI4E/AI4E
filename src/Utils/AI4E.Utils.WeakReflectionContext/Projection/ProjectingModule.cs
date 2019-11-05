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
    // Recursively 'projects' any assemblies, modules, types and members returned by a given module
    internal class ProjectingModule : DelegatingModule, IProjectable
    {
        public ProjectingModule(Module module, Projector projector)
            : base(module)
        {
            Debug.Assert(null != projector);

            Projector = projector;
        }

        public Projector Projector { get; }

        public override Assembly Assembly => Projector.ProjectAssembly(base.Assembly);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            attributeType = Projector.Unproject(attributeType);

            return base.GetCustomAttributes(attributeType, inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return Projector.Project(base.GetCustomAttributesData(), Projector.ProjectCustomAttributeData);
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            attributeType = Projector.Unproject(attributeType);

            return base.IsDefined(attributeType, inherit);
        }

        public override FieldInfo? GetField(string name, BindingFlags bindingAttr)
        {
            return Projector.ProjectField(base.GetField(name, bindingAttr));
        }

        public override FieldInfo[] GetFields(BindingFlags bindingFlags)
        {
            return Projector.Project(base.GetFields(bindingFlags), Projector.ProjectField!);
        }

        protected override MethodInfo? GetMethodImpl(
            string name, 
            BindingFlags bindingAttr,
            Binder? binder, 
            CallingConventions callConvention,
            Type[]? types, 
            ParameterModifier[]? modifiers)
        {
            types = Projector.Unproject(types);
            return Projector.ProjectMethod(
                base.GetMethodImpl(name, bindingAttr, binder, callConvention, types, modifiers));
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingFlags)
        {
            return Projector.Project(base.GetMethods(bindingFlags), Projector.ProjectMethod!);
        }

        public override Type? GetType(string className, bool throwOnError, bool ignoreCase)
        {
            return Projector.ProjectType(base.GetType(className, throwOnError, ignoreCase));
        }

        public override Type[] GetTypes()
        {
            return Projector.Project(base.GetTypes(), Projector.ProjectType!);
        }

        public override FieldInfo? ResolveField(
            int metadataToken, 
            Type[]? genericTypeArguments, 
            Type[]? genericMethodArguments)
        {
            genericTypeArguments = Projector.Unproject(genericTypeArguments);
            genericMethodArguments = Projector.Unproject(genericMethodArguments);

            return Projector.ProjectField(
                base.ResolveField(metadataToken, genericTypeArguments, genericMethodArguments));
        }

        public override MemberInfo? ResolveMember(
            int metadataToken, 
            Type[]? genericTypeArguments, 
            Type[]? genericMethodArguments)
        {
            genericTypeArguments = Projector.Unproject(genericTypeArguments);
            genericMethodArguments = Projector.Unproject(genericMethodArguments);

            return Projector.ProjectMember(
                base.ResolveMember(metadataToken, genericTypeArguments, genericMethodArguments));
        }

        public override MethodBase? ResolveMethod(
            int metadataToken, 
            Type[]? genericTypeArguments, 
            Type[]? genericMethodArguments)
        {
            genericTypeArguments = Projector.Unproject(genericTypeArguments);
            genericMethodArguments = Projector.Unproject(genericMethodArguments);

            return Projector.ProjectMethodBase(
                base.ResolveMethod(metadataToken, genericTypeArguments, genericMethodArguments));
        }

        public override Type ResolveType(
            int metadataToken, 
            Type[]? genericTypeArguments, 
            Type[]? genericMethodArguments)
        {
            genericTypeArguments = Projector.Unproject(genericTypeArguments);
            genericMethodArguments = Projector.Unproject(genericMethodArguments);

            return Projector.ProjectType(base.ResolveType(metadataToken, genericTypeArguments, genericMethodArguments));
        }

        public override bool Equals(object? o)
        {
            return o is ProjectingModule other &&
                   Projector == other.Projector &&
                   UnderlyingModule.Equals(other.UnderlyingModule);
        }

        public override int GetHashCode()
        {
            return Projector.GetHashCode() ^ UnderlyingModule.GetHashCode();
        }
    }
}
