using System;
using System.Collections.Generic;
using System.Linq;
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
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var session = (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            var path = GetPath(endPoint, session);

            await _coordinationManager.GetOrCreateAsync(path, _addressConversion.SerializeAddress(address), EntryCreationModes.Ephemeral, cancellation);
        }

        public async Task UnmapEndPointAsync(EndPointAddress endPoint, TAddress address, CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(address));

            var endPointEntry = await GetLogicalAddressEntryAsync(endPoint, cancellation);
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

            var entries = await endPointEntry.GetChildrenEntries()
#if !SUPPORTS_ASYNC_ENUMERABLE
                .ToArray(cancellation);
#else
                .ToArrayAsync(cancellation);
#endif

            return entries.Select(p => _addressConversion.DeserializeAddress(p.Value.ToArray()));
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
