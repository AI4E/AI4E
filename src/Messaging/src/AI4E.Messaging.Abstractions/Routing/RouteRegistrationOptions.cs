﻿/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System;

namespace AI4E.Messaging.Routing
{
    /// <summary>
    /// Specifies options for route registrations.
    /// </summary>
    [Flags]
    public enum RouteRegistrationOptions
    {
        /// <summary>
        /// The default route registration. The registration shall survive end-point shutdown until explicitely unregistered.
        /// </summary>
        Default = 0,

        /// <summary>
        /// The registration will be removed automatically, when the message router is disposed or the session terminates.
        /// </summary>
        Transient = 1,

        /// <summary>
        /// The handler shall not handle point to point messages, unless the message is not sent to the end-point via its address.
        /// </summary>
        PublishOnly = 2,

        /// <summary>
        /// The handler is target for messages that are dispatched from the same end-point only.
        /// </summary>
        LocalDispatchOnly = 4
    }
}
