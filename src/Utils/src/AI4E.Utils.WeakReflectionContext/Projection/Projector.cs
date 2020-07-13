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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml.Schema;

namespace AI4E.Utils.Projection
{
    internal abstract class Projector
    {
        [return: NotNullIfNotNull("values")]
        public IList<T>? Project<T>(IList<T>? values, Func<T, T> project)
        {
            if (values == null || values.Count == 0)
                return values;

            var projected = ProjectAll(values, project);

            return Array.AsReadOnly(projected);
        }

        [return: NotNullIfNotNull("values")]
        public T[]? Project<T>(T[]? values, Func<T, T> project)
        {
            if (values == null || values.Length == 0)
                return values;

            return ProjectAll(values, project);
        }

        [return: NotNullIfNotNull("value")]
        public T Project<T>(T value, Func<T, T> project)
        {
            if (!NeedsProjection(value!))
                return value;

            // NeedsProjection should guarantee this.
            Debug.Assert(!(value is IProjectable) || ((IProjectable)value).Projector != this);

            return project(value);
        }

        [return: NotNullIfNotNull("value")]
        public abstract TypeInfo? ProjectType(Type? value);

        [return: NotNullIfNotNull("value")]
        public abstract Assembly? ProjectAssembly(Assembly? value);

        [return: NotNullIfNotNull("value")]
        public abstract Module? ProjectModule(Module? value);

        [return: NotNullIfNotNull("value")]
        public abstract FieldInfo? ProjectField(FieldInfo? value);

        [return: NotNullIfNotNull("value")]
        public abstract EventInfo? ProjectEvent(EventInfo? value);

        [return: NotNullIfNotNull("value")]
        public abstract ConstructorInfo? ProjectConstructor(ConstructorInfo? value);

        [return: NotNullIfNotNull("value")]
        public abstract MethodInfo? ProjectMethod(MethodInfo? value);

        [return: NotNullIfNotNull("value")]
        public abstract MethodBase? ProjectMethodBase(MethodBase? value);

        [return: NotNullIfNotNull("value")]
        public abstract PropertyInfo? ProjectProperty(PropertyInfo? value);

        [return: NotNullIfNotNull("value")]
        public abstract ParameterInfo? ProjectParameter(ParameterInfo? value);

        [return: NotNullIfNotNull("value")]
        public abstract MethodBody? ProjectMethodBody(MethodBody? value);

        [return: NotNullIfNotNull("value")]
        public abstract LocalVariableInfo? ProjectLocalVariable(LocalVariableInfo? value);

        [return: NotNullIfNotNull("value")]
        public abstract ExceptionHandlingClause? ProjectExceptionHandlingClause(ExceptionHandlingClause? value);

        [return: NotNullIfNotNull("value")]
        public abstract CustomAttributeData? ProjectCustomAttributeData(CustomAttributeData? value);

        [return: NotNullIfNotNull("value")]
        public abstract ManifestResourceInfo? ProjectManifestResource(ManifestResourceInfo? value);

        public abstract CustomAttributeTypedArgument ProjectTypedArgument(CustomAttributeTypedArgument value);
        public abstract CustomAttributeNamedArgument ProjectNamedArgument(CustomAttributeNamedArgument value);
        public abstract InterfaceMapping ProjectInterfaceMapping(InterfaceMapping value);

        [return: NotNullIfNotNull("value")]
        public abstract MemberInfo? ProjectMember(MemberInfo? value);

        [return: NotNullIfNotNull("values")]
        public Type[]? Unproject(Type[]? values)
        {
            if (values == null)
                return null;

            var newTypes = new Type[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                newTypes[i] = Unproject(values[i]);
            }

            return newTypes;
        }

        [return: NotNullIfNotNull("value")]
        public Type? Unproject(Type? value)
        {
            if (value is ProjectingType projectingType)
                return projectingType.UnderlyingType;

            return value;
        }

        public bool NeedsProjection(object value)
        {
            Debug.Assert(value != null);

            if (value == null)
                return false;

            if (value is IProjectable projector && projector == this)
                return false;   // Already projected

            // Different context, so we need to project it
            return true;
        }

        //protected abstract object ExecuteProjection<T>(object value);

        //protected abstract IProjection GetProjector(Type t);

        private T[] ProjectAll<T>(IList<T> values, Func<T, T> project)
        {
            Debug.Assert(null != project);
            Debug.Assert(values != null && values.Count > 0);

            var projected = new T[values!.Count];

            for (var i = 0; i < projected.Length; i++)
            {
                var value = values[i];
                Debug.Assert(value != null && NeedsProjection(value));
                projected[i] = project!(value);
            }

            return projected;
        }
    }
}
