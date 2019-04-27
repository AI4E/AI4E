using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Remoting;
using AI4E.Routing;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity
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

        public async Task MapEndPointAsync(EndPointAddress endPoint, TAddress address, CancellationToken cancellation)
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

        public async Task UnmapEndPointAsync(EndPointAddress endPoint, TAddress address, CancellationToken cancellation)
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

        public async Task UnmapEndPointAsync(EndPointAddress endPoint, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            var path = GetPath(endPoint);

            await _coordinationManager.DeleteAsync(path, recursive: true, cancellation: cancellation);
        }

        public async ValueTask<IEnumerable<TAddress>> GetMapsAsync(EndPointAddress endPoint, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            var endPointEntry = await GetLogicalAddressEntryAsync(endPoint, cancellation);

            Assert(endPointEntry != null);

            var entries = await endPointEntry.GetChildrenEntries().ToArray(cancellation);

            return entries.Select(p => _physicalEndPoint.AddressFromString(Encoding.UTF8.GetString(p.Value.Span)));
        }

        #endregion

        private ValueTask<IEntry> GetLogicalAddressEntryAsync(EndPointAddress endPoint, CancellationToken cancellation)
        {
            var path = GetPath(endPoint);
            return _coordinationManager.GetOrCreateAsync(path, ReadOnlyMemory<byte>.Empty, EntryCreationModes.Default, cancellation);
        }

        private static CoordinationEntryPath GetPath(EndPointAddress endPoint)
        {
            return _mapsRootPath.GetChildPath(endPoint.ToString());
        }

        private static CoordinationEntryPath GetPath(EndPointAddress endPoint, string session)
        {
            return _mapsRootPath.GetChildPath(endPoint.ToString(), session);
        }
    }
}
