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


using System;

namespace AI4E
{
    /// <summary>
    /// Represents the context of a message dispatch operation.
    /// </summary>
    public interface IMessageDispatchContext
    {
        /// <summary>
        /// Gets the dispatch operations service provider.
        /// </summary>
        IServiceProvider DispatchServices { get; }

        /// <summary>
        /// Gets the dispatch data of the current dispatch operation.
        /// </summary>
        DispatchDataDictionary DispatchData { get; }

        /// <summary>
        /// Gets a boolean value specifying whether the message is published to all handlers.
        /// </summary>
        bool IsPublish { get; }

        /// <summary>
        /// Gets a boolean value specifying whether the message is dispatched locally.
        /// </summary>
        bool IsLocalDispatch { get; }
    }


    /// <summary>
    /// An attribute that identifies a message handler's context property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class MessageDispatchContextAttribute : Attribute { }
}
