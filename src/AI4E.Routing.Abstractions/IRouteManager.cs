/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IRouteManager.cs 
 * Types:           AI4E.Routing.IRouteManager
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   04.10.2018 
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Routing
{
    public interface IRouteManager
    {
        Task AddRouteAsync(EndPointAddress endPoint, Route route, RouteRegistrationOptions registrationOptions, CancellationToken cancellation = default);
        Task RemoveRouteAsync(EndPointAddress endPoint, Route route, CancellationToken cancellation = default);
        Task RemoveRoutesAsync(EndPointAddress endPoint, bool removePersistentRoutes, CancellationToken cancellation = default);

        // TODO: Rename to GetRouteTargets?
        Task<IEnumerable<RouteTarget>> GetRoutesAsync(Route route, CancellationToken cancellation = default);
    }

    public interface IRouteManagerFactory
    {
        IRouteManager CreateRouteManager();
    }
}
