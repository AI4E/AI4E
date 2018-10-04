using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Remoting;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public sealed class EndPointMap<TAddress> : IEndPointMap<TAddress>
    {
        private static readonly CoordinationEntryPath _mapsRootPath = new CoordinationEntryPath("maps");

        private readonly ICoordinationManager _coordinationManager;
        private readonly IAddressConversion<TAddress> _addressConversion;

        public EndPointMap(ICoordinationManager coordinationManager, IAddressConversion<TAddress> addressConversion)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _coordinationManager = coordinationManager;
            _addressConversion = addressConversion;
        }

        #region IEndPointMap<TAddress>

        public async Task MapEndPointAsync(EndPointAddress endPoint, TAddress address, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var logicalAddress = endPoint.LogicalAddress;
            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(logicalAddress, session);

            await _coordinationManager.GetOrCreateAsync(path, _addressConversion.SerializeAddress(address), EntryCreationModes.Ephemeral, cancellation);
        }

        public async Task UnmapEndPointAsync(EndPointAddress endPoint, TAddress address, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var logicalAddress = endPoint.LogicalAddress;
            var logicalAddressEntry = await GetLogicalAddressEntryAsync(logicalAddress, cancellation);
            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(logicalAddress, session);

            await _coordinationManager.DeleteAsync(path, cancellation: cancellation);
        }

        public async Task UnmapEndPointAsync(EndPointAddress endPoint, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            var logicalAddress = endPoint.LogicalAddress;
            var path = GetPath(logicalAddress);

            await _coordinationManager.DeleteAsync(path, recursive: true, cancellation: cancellation);
        }

        public async ValueTask<IEnumerable<TAddress>> GetMapsAsync(EndPointAddress endPoint, CancellationToken cancellation)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            var logicalAddress = endPoint.LogicalAddress;
            var logicalAddressEntry = await GetLogicalAddressEntryAsync(logicalAddress, cancellation);

            Assert(logicalAddressEntry != null);

            var entries = await logicalAddressEntry.GetChildrenEntries().ToArray(cancellation);

            return entries.Select(p => _addressConversion.DeserializeAddress(p.Value.ToArray()));
        }

        #endregion

        private ValueTask<IEntry> GetLogicalAddressEntryAsync(string logicalAddress, CancellationToken cancellation)
        {
            var path = GetPath(logicalAddress);
            return _coordinationManager.GetOrCreateAsync(path, ReadOnlyMemory<byte>.Empty, EntryCreationModes.Default, cancellation);
        }

        private static CoordinationEntryPath GetPath(string logicalAddress)
        {
            return _mapsRootPath.GetChildPath(logicalAddress);
        }

        private static CoordinationEntryPath GetPath(string logicalAddress, string session)
        {
            return _mapsRootPath.GetChildPath(logicalAddress, session);
        }
    }
}
