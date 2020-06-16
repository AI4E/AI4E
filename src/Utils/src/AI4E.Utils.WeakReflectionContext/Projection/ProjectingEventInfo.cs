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
    // Recursively 'projects' any assemblies, modules, types and members returned by a given event
    internal class ProjectingEventInfo : DelegatingEventInfo, IProjectable
    {
        public ProjectingEventInfo(EventInfo @event, Projector projector)
            : base(@event)
        {
            Debug.Assert(null != projector);

            Projector = projector!;
        }

        public Projector Projector { get; }

        public override Type? DeclaringType => Projector.ProjectType(base.DeclaringType);

        public override Type? EventHandlerType => Projector.ProjectType(base.EventHandlerType);

        public override Module Module => Projector.ProjectModule(base.Module);
        public override Type? ReflectedType => Projector.ProjectType(base.ReflectedType);

        public override MethodInfo? GetAddMethod(bool nonPublic)
        {
            return Projector.ProjectMethod(base.GetAddMethod(nonPublic));
        }

        public override MethodInfo[] GetOtherMethods(bool nonPublic)
        {
            return Projector.Project(base.GetOtherMethods(nonPublic), Projector.ProjectMethod!);
        }

        public override MethodInfo? GetRaiseMethod(bool nonPublic)
        {
            return Projector.ProjectMethod(base.GetRaiseMethod(nonPublic));
        }

        public override MethodInfo? GetRemoveMethod(bool nonPublic)
        {
            return Projector.ProjectMethod(base.GetRemoveMethod(nonPublic));
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

        public override bool Equals(object? o)
        {
            return o is ProjectingEventInfo other &&
                   Projector == other.Projector &&
                   UnderlyingEvent.Equals(other.UnderlyingEvent);
        }

        public override int GetHashCode()
        {
            return Projector.GetHashCode() ^ UnderlyingEvent.GetHashCode();
        }
    }
}
