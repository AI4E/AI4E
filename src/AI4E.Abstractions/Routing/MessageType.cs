/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MessageType.cs 
 * Types:           AI4E.Routing.MessageType
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
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


namespace AI4E.Routing
{
    public enum MessageType : int
    {
        /// <summary>
        /// An unknown message type.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A normal (user) message.
        /// </summary>
        Message = 1,

        /// <summary>
        /// A request for a (user) message.
        /// </summary>
        Request = 2,

        /// <summary>
        /// A signal that one or multiple (user) messages are available for request.
        /// </summary>
        Signal = 3,

        /// <summary>
        /// The protocol of a received message is not supported. The payload is the seq-num of the message in raw format.
        /// </summary>
        ProtocolNotSupported = -1,

        EndPointNotPresent = -2,

        Misrouted = -3
    }
}
