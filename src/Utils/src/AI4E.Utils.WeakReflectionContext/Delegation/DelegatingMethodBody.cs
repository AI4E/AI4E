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
    internal class DelegatingMethodBody : MethodBody
    {
        private readonly WeakReference<MethodBody> _body;

        public DelegatingMethodBody(MethodBody body)
        {
            Debug.Assert(null != body);

            _body = new WeakReference<MethodBody>(body!);
        }

        private MethodBody Body
        {
            get
            {
                if (_body.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override IList<ExceptionHandlingClause> ExceptionHandlingClauses => Body.ExceptionHandlingClauses;

        public override bool InitLocals => Body.InitLocals;

        public override int LocalSignatureMetadataToken => Body.LocalSignatureMetadataToken;

        public override IList<LocalVariableInfo> LocalVariables => Body.LocalVariables;

        public override int MaxStackSize => Body.MaxStackSize;

        public override byte[]? GetILAsByteArray()
        {
            return Body.GetILAsByteArray();
        }

        public override string? ToString()
        {
            return Body.ToString();
        }
    }
}
