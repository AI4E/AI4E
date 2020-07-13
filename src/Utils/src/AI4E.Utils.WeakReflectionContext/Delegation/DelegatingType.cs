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
using System.Runtime.InteropServices;

namespace AI4E.Utils.Delegation
{
    internal abstract class DelegatingType : TypeInfo
    {
        private readonly WeakReference<TypeInfo> _typeInfo;

        public DelegatingType(Type type)
        {
            Debug.Assert(null != type);

            var typeInfo = type.GetTypeInfo();
            if (typeInfo == null)
            {
                throw new InvalidOperationException($"Cannot get the TypeInfo object from the Type object: {type!.FullName}.");
            }

            _typeInfo = new WeakReference<TypeInfo>(typeInfo);
        }

        private TypeInfo TypeInfo
        {
            get
            {
                if (_typeInfo.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override Assembly Assembly => TypeInfo.Assembly;

        public override string? AssemblyQualifiedName => TypeInfo.AssemblyQualifiedName;

        public override Type? BaseType => TypeInfo.BaseType;

        public override bool ContainsGenericParameters => TypeInfo.ContainsGenericParameters;

        public override int GenericParameterPosition => TypeInfo.GenericParameterPosition;

        public override MethodBase? DeclaringMethod => TypeInfo.DeclaringMethod;

        public override Type? DeclaringType => TypeInfo.DeclaringType;

        public override string? FullName => TypeInfo.FullName;

        public override GenericParameterAttributes GenericParameterAttributes => TypeInfo.GenericParameterAttributes;

        public override Guid GUID => TypeInfo.GUID;

        public override bool IsSZArray => TypeInfo.IsSZArray;

        public override bool IsEnum => TypeInfo.IsEnum;

        public override bool IsGenericParameter => TypeInfo.IsGenericParameter;

        public override bool IsGenericType => TypeInfo.IsGenericType;

        public override bool IsGenericTypeDefinition => TypeInfo.IsGenericTypeDefinition;

        public override bool IsConstructedGenericType => TypeInfo.IsConstructedGenericType;

        public override bool IsSecurityCritical => TypeInfo.IsSecurityCritical;

        public override bool IsSecuritySafeCritical => TypeInfo.IsSecuritySafeCritical;

        public override bool IsSecurityTransparent => TypeInfo.IsSecurityTransparent;

        public override bool IsSerializable => TypeInfo.IsSerializable;

        public override int MetadataToken => TypeInfo.MetadataToken;

        public override Module Module => TypeInfo.Module;

        public override string Name => TypeInfo.Name;

        public override string? Namespace => TypeInfo.Namespace;

        public override Type? ReflectedType => TypeInfo.ReflectedType;

        public override StructLayoutAttribute? StructLayoutAttribute => TypeInfo.StructLayoutAttribute;

        public override RuntimeTypeHandle TypeHandle => TypeInfo.TypeHandle;

        public override Type UnderlyingSystemType => TypeInfo.UnderlyingSystemType;

        public Type UnderlyingType => TypeInfo;

        internal object Delegate => UnderlyingType;

        public override int GetArrayRank()
        {
            return TypeInfo.GetArrayRank();
        }

        public override MemberInfo[] GetDefaultMembers()
        {
            return TypeInfo.GetDefaultMembers();
        }

        public override string? GetEnumName(object value)
        {
            return TypeInfo.GetEnumName(value);
        }

        public override string[] GetEnumNames()
        {
            return TypeInfo.GetEnumNames();
        }

        public override Array GetEnumValues()
        {
            return TypeInfo.GetEnumValues();
        }

        public override Type GetEnumUnderlyingType()
        {
            return TypeInfo.GetEnumUnderlyingType();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return TypeInfo.GetCustomAttributes(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return TypeInfo.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return TypeInfo.GetCustomAttributesData();
        }

        public override EventInfo[] GetEvents()
        {
            return TypeInfo.GetEvents();
        }

        public override Type[] GetGenericArguments()
        {
            return TypeInfo.GetGenericArguments();
        }

        public override Type[] GetGenericParameterConstraints()
        {
            return TypeInfo.GetGenericParameterConstraints();
        }

        public override Type GetGenericTypeDefinition()
        {
            return TypeInfo.GetGenericTypeDefinition();
        }

        public override InterfaceMapping GetInterfaceMap(Type interfaceType)
        {
            return TypeInfo.GetInterfaceMap(interfaceType);
        }

        public override MemberInfo[] GetMember(string name, MemberTypes type, BindingFlags bindingAttr)
        {
            return TypeInfo.GetMember(name, type, bindingAttr);
        }

        protected override TypeCode GetTypeCodeImpl()
        {
            return Type.GetTypeCode(TypeInfo);
        }

        public override bool IsAssignableFrom(Type? c)
        {
            return TypeInfo.IsAssignableFrom(c);
        }

        protected override bool IsContextfulImpl()
        {
            return TypeInfo.IsContextful;
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return TypeInfo.IsDefined(attributeType, inherit);
        }

        public override bool IsEnumDefined(object value)
        {
            return TypeInfo.IsEnumDefined(value);
        }

        public override bool IsEquivalentTo(Type? other)
        {
            return TypeInfo.IsEquivalentTo(other);
        }

        public override bool IsInstanceOfType(object? o)
        {
            return TypeInfo.IsInstanceOfType(o);
        }

        protected override bool IsMarshalByRefImpl()
        {
            return TypeInfo.IsMarshalByRef;
        }

        // We could have used the default implementation of this on Type
        // if it handled special cases like generic type constraints
        // and interfaces->objec.
        public override bool IsSubclassOf(Type c)
        {
            return TypeInfo.IsSubclassOf(c);
        }

        protected override bool IsValueTypeImpl()
        {
            return TypeInfo.IsValueType;
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            return TypeInfo.Attributes;
        }

        protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers)
        {
            return TypeInfo.GetConstructor(bindingAttr, binder, callConvention, types, modifiers);
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            return TypeInfo.GetConstructors(bindingAttr);
        }

        public override Type? GetElementType()
        {
            return TypeInfo.GetElementType();
        }

        public override EventInfo? GetEvent(string name, BindingFlags bindingAttr)
        {
            return TypeInfo.GetEvent(name, bindingAttr);
        }

        public override EventInfo[] GetEvents(BindingFlags bindingAttr)
        {
            return TypeInfo.GetEvents(bindingAttr);
        }

        public override FieldInfo? GetField(string name, BindingFlags bindingAttr)
        {
            return TypeInfo.GetField(name, bindingAttr);
        }

        public override FieldInfo[] GetFields(BindingFlags bindingAttr)
        {
            return TypeInfo.GetFields(bindingAttr);
        }

        public override Type? GetInterface(string name, bool ignoreCase)
        {
            return TypeInfo.GetInterface(name, ignoreCase);
        }

        public override Type[] GetInterfaces()
        {
            return TypeInfo.GetInterfaces();
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
        {
            return TypeInfo.GetMembers(bindingAttr);
        }

        protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers)
        {
            // Unfortunately we cannot directly call the protected GetMethodImpl on _typeInfo.
            return (types is null) ?
                TypeInfo.GetMethod(name, bindingAttr) :
                TypeInfo.GetMethod(name, bindingAttr, binder, callConvention, types, modifiers);
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
        {
            return TypeInfo.GetMethods(bindingAttr);
        }

        public override Type? GetNestedType(string name, BindingFlags bindingAttr)
        {
            return TypeInfo.GetNestedType(name, bindingAttr);
        }

        public override Type[] GetNestedTypes(BindingFlags bindingAttr)
        {
            return TypeInfo.GetNestedTypes(bindingAttr);
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            return TypeInfo.GetProperties(bindingAttr);
        }

        protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers)
        {
            // Unfortunately we cannot directly call the protected GetPropertyImpl on _typeInfo.
            PropertyInfo? property;

            if (types == null)
            {
                // if types is null, we can ignore binder and modifiers
                if (returnType == null)
                {
                    property = TypeInfo.GetProperty(name, bindingAttr);
                }
                else
                {
                    // Ideally we should call a GetProperty overload that takes name, returnType, and bindingAttr, but not types.
                    // But such an overload doesn't exist. On the other hand, this also guarantees that bindingAttr will be
                    // the default lookup flags if types is null but returnType is not.
                    Debug.Assert(bindingAttr == (BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public));

                    property = TypeInfo.GetProperty(name, returnType);
                }
            }
            else
            {
                property = TypeInfo.GetProperty(name, bindingAttr, binder, returnType, types, modifiers);
            }

            return property;
        }

        protected override bool HasElementTypeImpl()
        {
            return TypeInfo.HasElementType;
        }

        public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters)
        {
            return TypeInfo.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);
        }

        protected override bool IsArrayImpl()
        {
            return TypeInfo.IsArray;
        }

        protected override bool IsByRefImpl()
        {
            return TypeInfo.IsByRef;
        }

        protected override bool IsCOMObjectImpl()
        {
            return TypeInfo.IsCOMObject;
        }

        protected override bool IsPointerImpl()
        {
            return TypeInfo.IsPointer;
        }

        protected override bool IsPrimitiveImpl()
        {
            return TypeInfo.IsPrimitive;
        }

        public override Type MakeArrayType()
        {
            return TypeInfo.MakeArrayType();
        }

        public override Type MakeArrayType(int rank)
        {
            return TypeInfo.MakeArrayType(rank);
        }

        public override Type MakePointerType()
        {
            return TypeInfo.MakePointerType();
        }

        public override Type MakeGenericType(params Type[] typeArguments)
        {
            return TypeInfo.MakeGenericType(typeArguments);
        }

        public override Type MakeByRefType()
        {
            return TypeInfo.MakeByRefType();
        }

        public override string ToString()
        {
            return TypeInfo.ToString();
        }
    }
}
