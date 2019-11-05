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

namespace AI4E.Utils.Delegation
{
    internal class DelegatingModule : Module
    {
        private readonly WeakReference<Module> _underlyingModule;

        public DelegatingModule(Module module)
        {
            Debug.Assert(null != module);

            _underlyingModule = new WeakReference<Module>(module);
        }

        public Module UnderlyingModule
        {
            get
            {
                if (_underlyingModule.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override Assembly Assembly => UnderlyingModule.Assembly;

        public override string FullyQualifiedName => UnderlyingModule.FullyQualifiedName;

        public override int MDStreamVersion => UnderlyingModule.MDStreamVersion;

        public override int MetadataToken => UnderlyingModule.MetadataToken;

        public override Guid ModuleVersionId => UnderlyingModule.ModuleVersionId;

        public override string Name => UnderlyingModule.Name;

        public override string ScopeName => UnderlyingModule.ScopeName;

        public override Type[] FindTypes(TypeFilter? filter, object? filterCriteria)
        {
            return UnderlyingModule.FindTypes(filter, filterCriteria);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return UnderlyingModule.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return UnderlyingModule.GetCustomAttributes(attributeType, inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return UnderlyingModule.GetCustomAttributesData();
        }

        public override FieldInfo? GetField(string name, BindingFlags bindingAttr)
        {
            return UnderlyingModule.GetField(name, bindingAttr);
        }

        public override FieldInfo[] GetFields(BindingFlags bindingFlags)
        {
            return UnderlyingModule.GetFields(bindingFlags);
        }

        protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers)
        {
            if (types == null)
            {
                return UnderlyingModule.GetMethod(name);
            }

            return UnderlyingModule.GetMethod(name, bindingAttr, binder, callConvention, types, modifiers);
        }

        public override MethodInfo[] GetMethods(BindingFlags bindingFlags)
        {
            return UnderlyingModule.GetMethods(bindingFlags);
        }

        public override void GetPEKind(out PortableExecutableKinds peKind, out ImageFileMachine machine)
        {
            UnderlyingModule.GetPEKind(out peKind, out machine);
        }

        //public override X509Certificate GetSignerCertificate()
        //{
        //    return UnderlyingModule.GetSignerCertificate();
        //}

        public override Type? GetType(string className, bool throwOnError, bool ignoreCase)
        {
            return UnderlyingModule.GetType(className, throwOnError, ignoreCase);
        }

        public override Type[] GetTypes()
        {
            return UnderlyingModule.GetTypes();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return UnderlyingModule.IsDefined(attributeType, inherit);
        }

        public override bool IsResource()
        {
            return UnderlyingModule.IsResource();
        }

        public override FieldInfo? ResolveField(int metadataToken, Type[]? genericTypeArguments, Type[]? genericMethodArguments)
        {
            return UnderlyingModule.ResolveField(metadataToken, genericTypeArguments, genericMethodArguments);
        }

        public override MemberInfo? ResolveMember(int metadataToken, Type[]? genericTypeArguments, Type[]? genericMethodArguments)
        {
            return UnderlyingModule.ResolveMember(metadataToken, genericTypeArguments, genericMethodArguments);
        }

        public override MethodBase? ResolveMethod(int metadataToken, Type[]? genericTypeArguments, Type[]? genericMethodArguments)
        {
            return UnderlyingModule.ResolveMethod(metadataToken, genericTypeArguments, genericMethodArguments);
        }

        public override byte[] ResolveSignature(int metadataToken)
        {
            return UnderlyingModule.ResolveSignature(metadataToken);
        }

        public override string ResolveString(int metadataToken)
        {
            return UnderlyingModule.ResolveString(metadataToken);
        }

        public override Type ResolveType(int metadataToken, Type[]? genericTypeArguments, Type[]? genericMethodArguments)
        {
            return UnderlyingModule.ResolveType(metadataToken, genericTypeArguments, genericMethodArguments);
        }

        public override string ToString()
        {
            return UnderlyingModule.ToString();
        }
    }
}
