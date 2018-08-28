using System;
using System.Net;
using AI4E.Remoting;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugModuleConnectedHandler : MessageHandler
    {
        private readonly DebugPort _debugPort;
        private readonly IAddressConversion<IPEndPoint> _addressConversion;

        public DebugModuleConnectedHandler(DebugPort debugPort, IAddressConversion<IPEndPoint> addressConversion)
        {
            if (debugPort == null)
                throw new ArgumentNullException(nameof(debugPort));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _debugPort = debugPort;
            _addressConversion = addressConversion;
        }

        public void Handle(DebugModuleConnected message)
        {
            _debugPort.DebugSessionConnected(_addressConversion.DeserializeAddress(message.Address),
                                             message.EndPoint,
                                             message.Module,
                                             message.ModuleVersion);
        }
    }
}
