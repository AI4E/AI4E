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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Coordination;
using AI4E.Messaging.Routing;
using AI4E.Remoting;
using static System.Diagnostics.Debug;

namespace AI4E.Messaging.Remote
{
    public sealed class EndPointMap<TAddress> : IEndPointMap<TAddress>
    {
        private static readonly CoordinationEntryPath _mapsRootPath = new CoordinationEntryPath("maps");

        private readonly ICoordinationManager _coordinationManager;
        private readonly IPhysicalEndPoint<TAddress> _physicalEndPoint;

        public EndPointMap(ICoordinationManager coordinationManager, IPhysicalEndPoint<TAddress> physicalEndPoint)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            _coordinationManager = coordinationManager;
            _physicalEndPoint = physicalEndPoint;
        }

        #region IEndPointMap<TAddress>

        public async Task MapEndPointAsync(RouteEndPointAddress endPoint, TAddress address, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(endPoint, session);

            await _coordinationManager.GetOrCreateAsync(path, Encoding.UTF8.GetBytes(_physicalEndPoint.AddressToString(address)), EntryCreationModes.Ephemeral, cancellation);
        }

        public async Task UnmapEndPointAsync(RouteEndPointAddress endPoint, TAddress address, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var endPointEntry = await GetLogicalAddressEntryAsync(endPoint, cancellation); // TODO: This is never used?!
            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(endPoint, session);

            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);
        }

        public async Task UnmapEndPointAsync(RouteEndPointAddress endPoint, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            var path = GetPath(endPoint);

            await _coordinationManager.DeleteAsync(path, recursive: true, cancellation: cancellation);
        }

        public async ValueTask<IEnumerable<TAddress>> GetMapsAsync(RouteEndPointAddress endPoint, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            var endPointEntry = await GetLogicalAddressEntryAsync(endPoint, cancellation);

            Assert(endPointEntry != null);

            var entries = await endPointEntry.GetChildrenEntries().ToArrayAsync(cancellation);

            return entries.Select(p => _physicalEndPoint.AddressFromString(Encoding.UTF8.GetString(p.Value.Span)));
        }

        #endregion

        private ValueTask<IEntry> GetLogicalAddressEntryAsync(RouteEndPointAddress endPoint, CancellationToken cancellation)
        {
            var path = GetPath(endPoint);
            return _coordinationManager.GetOrCreateAsync(path, ReadOnlyMemory<byte>.Empty, EntryCreationModes.Default, cancellation);
        }

        private static CoordinationEntryPath GetPath(RouteEndPointAddress endPoint)
        {
            return _mapsRootPath.GetChildPath(endPoint.ToString());
        }

        private static CoordinationEntryPath GetPath(RouteEndPointAddress endPoint, string session)
        {
            return _mapsRootPath.GetChildPath(endPoint.ToString(), session);
        }
    }
}
