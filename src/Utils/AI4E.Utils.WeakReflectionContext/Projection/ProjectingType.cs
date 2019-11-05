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
    // Recursively 'projects' any assemblies, modules, types and members returned by a given type
    internal class ProjectingType : DelegatingType, IProjectable
    {
        public ProjectingType(Type type, Projector projector)
            :  base(type)
        {
            Debug.Assert(null != projector);

            Projector = projector!;
        }

        public Projector Projector { get; private set; }

        public override Assembly Assembly => Projector.ProjectAssembly(base.Assembly);

        public override Type? BaseType => Projector.ProjectType(base.BaseType);

        public override MethodBase? DeclaringMethod => Projector.ProjectMethodBase(base.DeclaringMethod);

        public override Type? DeclaringType => Projector.ProjectType(base.DeclaringType);

        public override Module Module => Projector.ProjectModule(base.Module);

        public override Type? ReflectedType => Projector.ProjectType(base.ReflectedType);

        public override MemberInfo[] GetDefaultMembers()
        {
            return Projector.Project(base.GetDefaultMembers(), Projector.ProjectMember!);
        }

        public override Type GetEnumUnderlyingType()
        {
            return Projector.ProjectType(base.GetEnumUnderlyingType());
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

        public override EventInfo[] GetEvents()
        {
            return Projector.Project(base.GetEvents(), Projector.ProjectEvent!);
        }

        public override Type[] GetGenericArguments()
        {
            return Projector.Project(base.GetGenericArguments(), Projector.ProjectType!);
        }

        public override Type[] GetGenericParameterConstraints()
        {
            return Projector.Project(base.GetGenericParameterConstraints(), Projector.ProjectType!);
        }

        public override Type GetGenericTypeDefinition()
        {
            return Projector.ProjectType(base.GetGenericTypeDefinition());
        }

        public override InterfaceMapping GetInterfaceMap(Type interfaceType)
        {
            interfaceType = Projector.Unproject(interfaceType);

            return Projector.ProjectInterfaceMapping(base.GetInterfaceMap(interfaceType));
        }

        public override MemberInfo[] GetMember(string name, MemberTypes type, BindingFlags bindingAttr)
        {
            var comparisonType = (bindingAttr & BindingFlags.IgnoreCase) == BindingFlags.IgnoreCase
                ? StringComparison.OrdinalIgnoreCase 
                : StringComparison.Ordinal;

            var matchingMembers = new List<MemberInfo>();

            if ((type & MemberTypes.Constructor) != 0)
                matchingMembers.AddRange(GetConstructors(bindingAttr));

            if ((type & MemberTypes.Event) != 0)
                matchingMembers.AddRange(GetEvents(bindingAttr));

            if ((type & MemberTypes.Field) != 0)
                matchingMembers.AddRange(GetFields(bindingAttr));

            if ((type & MemberTypes.Method) != 0)
                matchingMembers.AddRange(GetMethods(bindingAttr));

            if ((type & MemberTypes.NestedType) != 0)
                matchingMembers.AddRange(GetNestedTypes(bindingAttr));

            if ((type & MemberTypes.Property) != 0)
                matchingMembers.AddRange(GetProperties(bindingAttr));

            matchingMembers.RemoveAll(member => !string.Equals(member.Name, name, comparisonType));

            return matchingMembers.ToArray();
        }

        public override bool IsAssignableFrom(Type? c)
        {
            if (c is ProjectingType otherType && Projector == otherType.Projector)
                return UnderlyingType.IsAssignableFrom(otherType.UnderlyingType);

            return false;
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            attributeType = Projector.Unproject(attributeType);

            return base.IsDefined(attributeType, inherit);
        }

        public override bool IsEquivalentTo(Type? other)
        {
            if (other is ProjectingType otherType && Projector == otherType.Projector)
                return UnderlyingType.IsEquivalentTo(otherType.UnderlyingType);

            return false;
        }

        public override bool IsInstanceOfType(object? o)
        {
            Type? objectType = Projector.ProjectType(o?.GetType());

            return IsAssignableFrom(objectType);
        }

        // We could have used the default implementation of this on Type
        // if it handled special cases like generic type constraints
        // and interfaces->objec.
        public override bool IsSubclassOf(Type c)
        {
            if (c is ProjectingType otherType && Projector == otherType.Projector)
                return UnderlyingType.IsSubclassOf(otherType.UnderlyingType);

            return false;
        }

        protected override ConstructorInfo? GetConstructorImpl(
            BindingFlags bindingAttr, 
            Binder? binder, 
            CallingConventions callConvention, 
            Type[] types, 
            ParameterModifier[]? modifiers)
        {
            types = Projector.Unproject(types);

            return Projector.ProjectConstructor(
                base.GetConstructorImpl(bindingAttr, binder, callConvention, types, modifiers));
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            return Projector.Project(base.GetConstructors(bindingAttr), Projector.ProjectConstructor!);
        }

        public override Type? GetElementType()
        {
            return Projector.ProjectType(base.GetElementType());
        }

        public override EventInfo? GetEvent(string name, BindingFlags bindingAttr)
        {
            return Projector.ProjectEvent(base.GetEvent(name, bindingAttr));
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            return Projector.Project(base.GetEvents(bindingAttr), Projector.ProjectEvent!);
        }

        public override FieldInfo? GetField(string name, BindingFlags bindingAttr)
        {
            return Projector.ProjectField(base.GetField(name, bindingAttr));
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            return Projector.Project(base.GetFields(bindingAttr), Projector.ProjectField!);
        }

        public override Type? GetInterface(string name, bool ignoreCase)
        {
            return Projector.ProjectType(base.GetInterface(name, ignoreCase));
        }

        public override Type[] GetInterfaces()
        {
            return Projector.Project(base.GetInterfaces(), Projector.ProjectType!);
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            var methods = GetMethods(bindingAttr);
            var constructors = GetConstructors(bindingAttr);
            var properties = GetProperties(bindingAttr);
            var events = GetEvents(bindingAttr);
            var fields = GetFields(bindingAttr);
            var nestedTypes = GetNestedTypes(bindingAttr);
            // Interfaces are excluded from the result of GetMembers

            var members = new MemberInfo[
                methods.Length +
                constructors.Length +
                properties.Length +
                events.Length +
                fields.Length +
                nestedTypes.Length];

            var i = 0;
            Array.Copy(methods, 0, members, i, methods.Length); i += methods.Length;
            Array.Copy(constructors, 0, members, i, constructors.Length); i += constructors.Length;
            Array.Copy(properties, 0, members, i, properties.Length); i += properties.Length;
            Array.Copy(events, 0, members, i, events.Length); i += events.Length;
            Array.Copy(fields, 0, members, i, fields.Length); i += fields.Length;
            Array.Copy(nestedTypes, 0, members, i, nestedTypes.Length); i += nestedTypes.Length;

            Debug.Assert(i == members.Length);

            return members;
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

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            return Projector.Project(base.GetMethods(bindingAttr), Projector.ProjectMethod!);
        }

        public override Type? GetNestedType(string name, BindingFlags bindingAttr)
        {
            return Projector.ProjectType(base.GetNestedType(name, bindingAttr));
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            return Projector.Project(base.GetNestedTypes(bindingAttr), Projector.ProjectType!);
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            return Projector.Project(base.GetProperties(bindingAttr), Projector.ProjectProperty!);
        }

        protected override PropertyInfo? GetPropertyImpl(
            string name, 
            BindingFlags bindingAttr, 
            Binder? binder, 
            Type? returnType, 
            Type[]? types, 
            ParameterModifier[]? modifiers)
        {
            returnType = Projector.Unproject(returnType);
            types = Projector.Unproject(types);

            return Projector.ProjectProperty(
                base.GetPropertyImpl(name, bindingAttr, binder, returnType, types, modifiers));
        }

        public override Type MakeArrayType()
        {
            return Projector.ProjectType(base.MakeArrayType());
        }

        public override Type MakeArrayType(int rank)
        {
            return Projector.ProjectType(base.MakeArrayType(rank));
        }

        public override Type MakePointerType()
        {
            return Projector.ProjectType(base.MakePointerType());
        }

        public override Type MakeGenericType(params Type[] typeArguments)
        {
            typeArguments = Projector.Unproject(typeArguments);

            return Projector.ProjectType(base.MakeGenericType(typeArguments));
        }

        public override Type MakeByRefType()
        {
            return Projector.ProjectType(base.MakeByRefType());
        }

        public override bool Equals(object? o)
        {
            return o is ProjectingType other &&
                Projector == other.Projector &&
                UnderlyingType.Equals(other.UnderlyingType);
        }

        public override int GetHashCode()
        {
            return Projector.GetHashCode() ^ UnderlyingType.GetHashCode();
        }
    }
}
