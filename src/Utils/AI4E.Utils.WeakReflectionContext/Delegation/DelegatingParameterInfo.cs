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
    internal class DelegatingParameterInfo : ParameterInfo
    {
        private readonly WeakReference<ParameterInfo> _underlyingParameter;

        public DelegatingParameterInfo(ParameterInfo parameter)
        {
            Debug.Assert(null != parameter);

            _underlyingParameter = new WeakReference<ParameterInfo>(parameter!);
        }

        public ParameterInfo UnderlyingParameter
        {
            get
            {
                if (_underlyingParameter.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override ParameterAttributes Attributes => UnderlyingParameter.Attributes;

        public override object? DefaultValue => UnderlyingParameter.DefaultValue;

        public override MemberInfo Member => UnderlyingParameter.Member;

        public override int MetadataToken => UnderlyingParameter.MetadataToken;

        public override string? Name => UnderlyingParameter.Name;

        public override Type ParameterType => UnderlyingParameter.ParameterType;

        public override int Position => UnderlyingParameter.Position;

        public override object? RawDefaultValue => UnderlyingParameter.RawDefaultValue;

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return UnderlyingParameter.GetCustomAttributes(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return UnderlyingParameter.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return UnderlyingParameter.GetCustomAttributesData();
        }

        public override Type[] GetOptionalCustomModifiers()
        {
            return UnderlyingParameter.GetOptionalCustomModifiers();
        }

        public override Type[] GetRequiredCustomModifiers()
        {
            return UnderlyingParameter.GetRequiredCustomModifiers();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return UnderlyingParameter.IsDefined(attributeType, inherit);
        }

        public override string ToString()
        {
            return UnderlyingParameter.ToString();
        }
    }
}
