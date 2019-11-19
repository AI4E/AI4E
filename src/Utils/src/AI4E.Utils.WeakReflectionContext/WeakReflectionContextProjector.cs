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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AI4E.Utils.Custom;
using AI4E.Utils.Delegation;
using AI4E.Utils.Projection;

namespace AI4E.Utils
{
    internal class WeakReflectionContextProjector : Projector
    {
        public WeakReflectionContextProjector(WeakReflectionContext context)
        {
            ReflectionContext = context;
        }

        public WeakReflectionContext ReflectionContext { get; }

        public TypeInfo ProjectTypeIfNeeded(TypeInfo value)
        {
            if (!NeedsProjection(value))
                return value;

            // Map the assembly to the underlying context first
            Debug.Assert(ReflectionContext.SourceContext != null);
            value = ReflectionContext.SourceContext!.MapType(value);
            return ProjectType(value);
        }

        public Assembly ProjectAssemblyIfNeeded(Assembly value)
        {
            if (!NeedsProjection(value))
                return value;

            // Map the assembly to the underlying context first
            Debug.Assert(ReflectionContext.SourceContext != null);
            value = ReflectionContext.SourceContext!.MapAssembly(value);
            return ProjectAssembly(value);
        }

        [return: NotNullIfNotNull("value")]
        public override TypeInfo? ProjectType(Type? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            var type = new CustomType(value, ReflectionContext);

#if SUPPORTS_INHERIT_TYPE_INFO
            return type;    
#else
            return new ProjectingTypeWrapper(type);
#endif
        }

        [return: NotNullIfNotNull("value")]
        public override Assembly? ProjectAssembly(Assembly? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new CustomAssembly(value, ReflectionContext);
        }

        [return: NotNullIfNotNull("value")]
        public override Module? ProjectModule(Module? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));

            return new CustomModule(value, ReflectionContext);
        }

        [return: NotNullIfNotNull("value")]
        public override FieldInfo? ProjectField(FieldInfo? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new CustomFieldInfo(value, ReflectionContext);
        }

        [return: NotNullIfNotNull("value")]
        public override EventInfo? ProjectEvent(EventInfo? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new CustomEventInfo(value, ReflectionContext);
        }

        [return: NotNullIfNotNull("value")]
        public override ConstructorInfo? ProjectConstructor(ConstructorInfo? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new CustomConstructorInfo(value, ReflectionContext);
        }

        [return: NotNullIfNotNull("value")]
        public override MethodInfo? ProjectMethod(MethodInfo? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new CustomMethodInfo(value, ReflectionContext);
        }

        [return: NotNullIfNotNull("value")]
        public override MethodBase? ProjectMethodBase(MethodBase? value)
        {
            if (value is null)
                return null;

            if (value is MethodInfo method)
                return ProjectMethod(method);

            if (value is ConstructorInfo constructor)
                return ProjectConstructor(constructor);

            throw new InvalidOperationException(
                $"The method is neither a MethodInfo nor a ConstructorInfo: {value.GetType()}.");
        }

        [return: NotNullIfNotNull("value")]
        public override PropertyInfo? ProjectProperty(PropertyInfo? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new CustomPropertyInfo(value, ReflectionContext);
        }

        [return: NotNullIfNotNull("value")]
        public override ParameterInfo? ProjectParameter(ParameterInfo? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new CustomParameterInfo(value, ReflectionContext);
        }

        [return: NotNullIfNotNull("value")]
        public override MethodBody? ProjectMethodBody(MethodBody? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new ProjectingMethodBody(value, this);
        }

        [return: NotNullIfNotNull("value")]
        public override LocalVariableInfo? ProjectLocalVariable(LocalVariableInfo? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new ProjectingLocalVariableInfo(value, this);
        }

        [return: NotNullIfNotNull("value")]
        public override ExceptionHandlingClause? ProjectExceptionHandlingClause(ExceptionHandlingClause? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new ProjectingExceptionHandlingClause(value, this);
        }

        [return: NotNullIfNotNull("value")]
        public override CustomAttributeData? ProjectCustomAttributeData(CustomAttributeData? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new ProjectingCustomAttributeData(value, this);
        }

        [return: NotNullIfNotNull("value")]
        public override ManifestResourceInfo? ProjectManifestResource(ManifestResourceInfo? value)
        {
            if (value is null)
                return null;

            Debug.Assert(NeedsProjection(value));
            return new ProjectingManifestResourceInfo(value, this);
        }

        [return: NotNullIfNotNull("value")]
        public override MemberInfo? ProjectMember(MemberInfo? value)
        {
            if (value is null)
                return null;

            return value.MemberType switch
            {
                var type when type == MemberTypes.TypeInfo || type == MemberTypes.NestedType
                => ProjectType((Type)value),
                MemberTypes.Constructor => ProjectConstructor((ConstructorInfo)value),
                MemberTypes.Event => ProjectEvent((EventInfo)value),
                MemberTypes.Field => ProjectField((FieldInfo)value),
                MemberTypes.Method => ProjectMethod((MethodInfo)value),
                MemberTypes.Property => ProjectProperty((PropertyInfo)value),
                _ => throw new InvalidOperationException(
                    $"The member {value.Name} has an invalid MemberType {value.MemberType}.")
            };
        }

        public override CustomAttributeTypedArgument ProjectTypedArgument(CustomAttributeTypedArgument value)
        {
            Type argumentType = ProjectType(value.ArgumentType);
            return new CustomAttributeTypedArgument(argumentType, value.Value);
        }

        public override CustomAttributeNamedArgument ProjectNamedArgument(CustomAttributeNamedArgument value)
        {
            var member = ProjectMember(value.MemberInfo);
            var typedArgument = ProjectTypedArgument(value.TypedValue);

            return new CustomAttributeNamedArgument(member, typedArgument);
        }

        public override InterfaceMapping ProjectInterfaceMapping(InterfaceMapping value)
        {
            return new InterfaceMapping
            {
                InterfaceMethods = Project(value.InterfaceMethods, ProjectMethod!),
                InterfaceType = ProjectType(value.InterfaceType),
                TargetMethods = Project(value.TargetMethods, ProjectMethod!),
                TargetType = ProjectType(value.TargetType)
            };
        }
    }
}
