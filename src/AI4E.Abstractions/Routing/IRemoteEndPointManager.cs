/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IRemoteEndPointManager.cs 
 * Types:           AI4E.Routing.IRemoteEndPointManager'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   09.05.2018 
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
    /// <summary>
    /// Represents a manager that manages views of remote logical end points.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used in the protocol stack.</typeparam>
    /// <remarks>
    /// This type is not meant to be consumed directly but is part of the infrastructure to enable the remote message dispatching system.
    /// </remarks>
    public interface IRemoteEndPointManager<TAddress>
    {
        /// <summary>
        /// Gets the remote end point for the specified route.
        /// </summary>
        /// <param name="remoteEndPoint">The route of the remote end point.</param>
        /// <returns>An instance that represents the remote end point.</returns>
        IRemoteEndPoint<TAddress> GetRemoteEndPoint(EndPointRoute remoteEndPoint);
    }
}
