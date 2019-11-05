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
    internal class DelegatingEventInfo : EventInfo
    {
        private readonly WeakReference<EventInfo> _underlyingEvent;

        public DelegatingEventInfo(EventInfo @event)
        {
            Debug.Assert(null != @event);

            _underlyingEvent = new WeakReference<EventInfo>(@event);
        }

        public EventInfo UnderlyingEvent
        {
            get
            {
                if (_underlyingEvent.TryGetTarget(out var result))
                {
                    return result;
                }

                throw new ContextUnloadedException();
            }
        }

        public override EventAttributes Attributes => UnderlyingEvent.Attributes;

        public override Type? DeclaringType => UnderlyingEvent.DeclaringType;

        public override Type? EventHandlerType => UnderlyingEvent.EventHandlerType;

        public override bool IsMulticast => UnderlyingEvent.IsMulticast;

        public override int MetadataToken => UnderlyingEvent.MetadataToken;

        public override Module Module => UnderlyingEvent.Module;

        public override string Name => UnderlyingEvent.Name;

        public override Type? ReflectedType => UnderlyingEvent.ReflectedType;

        public override void AddEventHandler(object? target, Delegate? handler)
        {
            UnderlyingEvent.AddEventHandler(target, handler);
        }

        public override MethodInfo? GetAddMethod(bool nonPublic)
        {
            return UnderlyingEvent.GetAddMethod(nonPublic);
        }

        public override MethodInfo[] GetOtherMethods(bool nonPublic)
        {
            return UnderlyingEvent.GetOtherMethods(nonPublic);
        }

        public override MethodInfo? GetRaiseMethod(bool nonPublic)
        {
            return UnderlyingEvent.GetRaiseMethod(nonPublic);
        }

        public override MethodInfo? GetRemoveMethod(bool nonPublic)
        {
            return UnderlyingEvent.GetRemoveMethod(nonPublic);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return UnderlyingEvent.GetCustomAttributes(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return UnderlyingEvent.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return UnderlyingEvent.GetCustomAttributesData();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return UnderlyingEvent.IsDefined(attributeType, inherit);
        }

        public override void RemoveEventHandler(object? target, Delegate? handler)
        {
            UnderlyingEvent.RemoveEventHandler(target, handler);
        }

        public override string? ToString()
        {
            return UnderlyingEvent.ToString();
        }
    }
}
