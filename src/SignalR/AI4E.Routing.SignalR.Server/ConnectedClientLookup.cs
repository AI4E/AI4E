using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;

namespace AI4E.Routing.SignalR.Server
{
    // TODO: (1) Garbage collection of entries
    //       (2) Forcing an override in ValidateClientAsync may override a later lease end with an earlier lease end.
    public sealed class ConnectedClientLookup : IConnectedClientLookup
    {
        private const string _basePath = "/connectedClients/";
        private static readonly TimeSpan _leaseLength = TimeSpan.FromMinutes(10); // TODO: This should be configurable.

        private readonly ICoordinationManager _coordinationManager;
        private readonly IDateTimeProvider _dateTimeProvider;

        public ConnectedClientLookup(ICoordinationManager coordinationManager, IDateTimeProvider dateTimeProvider)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _coordinationManager = coordinationManager;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<(EndPointRoute endPoint, string securityToken)> AddClientAsync(CancellationToken cancellation)
        {
            var endPoint = new EndPointRoute("client/" + Guid.NewGuid().ToString());
            var securityToken = Guid.NewGuid().ToString();
            var leaseEnd = _dateTimeProvider.GetCurrentTime() + _leaseLength;

            byte[] bytes;

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    var securityTokenBytes = Encoding.UTF8.GetBytes(securityToken);

                    writer.Write(securityTokenBytes.Length);
                    writer.Write(securityTokenBytes);
                    writer.Write(leaseEnd.Ticks);
                }
                bytes = stream.ToArray();
            }

            do
            {
                try
                {
                    await _coordinationManager.CreateAsync(EntryPathHelper.GetChildPath(_basePath, endPoint.Route), bytes, cancellation: cancellation);

                    return (endPoint, securityToken);
                }
                catch (DuplicateEntryException) // TODO: Add a TryCreateAsync method to the coordination service.
                {
                    continue;
                }
            }
            while (true);
        }

        public async Task<bool> ValidateClientAsync(EndPointRoute endPoint, string securityToken, CancellationToken cancellation)
        {
            var path = EntryPathHelper.GetChildPath(_basePath, endPoint.Route);
            var entry = await _coordinationManager.GetAsync(path, cancellation: cancellation);

            if (entry == null)
            {
                return false;
            }

            byte[] securityTokenBytes;
            string comparandSecurityToken;

            using (var stream = new MemoryStream(entry.Value.ToArray()))
            using (var reader = new BinaryReader(stream))
            {
                var securityTokenBytesLength = reader.ReadInt32();
                securityTokenBytes = reader.ReadBytes(securityTokenBytesLength);
                comparandSecurityToken = Encoding.UTF8.GetString(securityTokenBytes);
            }

            if (securityToken != comparandSecurityToken)
            {
                return false;
            }

            var now = _dateTimeProvider.GetCurrentTime();
            var leaseEnd = now + _leaseLength;
            byte[] bytes;

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(securityTokenBytes.Length);
                    writer.Write(securityTokenBytes);
                    writer.Write(leaseEnd.Ticks);
                }
                bytes = stream.ToArray();
            }

            await _coordinationManager.SetValueAsync(path, bytes, cancellation: cancellation);
            return true;
        }
    }
}
