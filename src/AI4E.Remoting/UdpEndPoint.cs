using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using Microsoft.Extensions.Logging;

namespace AI4E.Remoting
{
    public sealed class UdpEndPoint : IPhysicalEndPoint<IPEndPoint>
    {
#pragma warning disable IDE1006
        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms740668(v=vs.85).aspx
        private const int WSAECONNRESET = 10054;
#pragma warning restore IDE1006

        private readonly ILogger<UdpEndPoint> _logger;
        private readonly UdpClient _udpClient;
        private readonly ConcurrentBag<(IMessage message, IPEndPoint remoteAddress)> _rxQueue;

        private bool _isDisposed = false;

        public UdpEndPoint(ILogger<UdpEndPoint> logger)
        {
            _logger = logger;
            _rxQueue = new ConcurrentBag<(IMessage message, IPEndPoint remoteAddress)>();

            var localAddress = GetLocalAddress();

            if (localAddress == null)
            {
                throw new Exception("Cannot evaluate local address."); // TODO: https://github.com/AI4E/AI4E/issues/32
            }

            // We generate an IPv4 end-point for now.
            // TODO: https://github.com/AI4E/AI4E/issues/30
            _udpClient = new UdpClient(new IPEndPoint(localAddress, port: 0));

            LocalAddress = new IPEndPoint(localAddress, ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // TODO: Does this thing work in linux/unix too?
                //       https://github.com/AI4E/AI4E/issues/29
                // See: https://stackoverflow.com/questions/7201862/an-existing-connection-was-forcibly-closed-by-the-remote-host
                uint IOC_IN = 0x80000000,
                     IOC_VENDOR = 0x18000000,
                     SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                _udpClient.Client.IOControl(unchecked((int)SIO_UDP_CONNRESET), new byte[] { Convert.ToByte(false) }, null);
            }

        }

        public async Task SendAsync(IMessage message, IPEndPoint remoteAddress, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (remoteAddress == null)
                throw new ArgumentNullException(nameof(remoteAddress));

            if (remoteAddress.Equals(LocalAddress))
            {
                _rxQueue.Add((message, LocalAddress));
                return;
            }

            var buffer = new byte[message.Length];
            using (var memoryStream = new MemoryStream(buffer, writable: true))
            {
                await message.WriteAsync(memoryStream, cancellation);
            }

            await _udpClient.SendAsync(buffer, buffer.Length, remoteAddress).WithCancellation(cancellation);
        }

        public async Task<(IMessage message, IPEndPoint remoteAddress)> ReceiveAsync(CancellationToken cancellation)
        {
            if (_rxQueue.TryTake(out var entry))
            {
                return entry;
            }

            UdpReceiveResult receiveResult;
            while (true)
            {
                try
                {
                    receiveResult = await _udpClient.ReceiveAsync().WithCancellation(cancellation);
                }
                // Apparently, the udp socket does receive ICMP messages that a remote host was unreachable on sending
                // and throws an exception on the next receive call. We just ignore this currently.
                // https://msdn.microsoft.com/en-us/library/windows/desktop/ms740120%28v=vs.85%29.aspx
                catch (SocketException exc) when (exc.ErrorCode == WSAECONNRESET)
                {
                    continue;
                }

                break;
            }

            var message = new Message();

            using (var memoryStream = new MemoryStream(receiveResult.Buffer))
            {
                await message.ReadAsync(memoryStream, cancellation);
            }

            return (message, receiveResult.RemoteEndPoint);
        }

        public IPEndPoint LocalAddress { get; }
        private IPAddress GetLocalAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                // TODO: https://github.com/AI4E/AI4E/issues/31
                // TODO: https://github.com/AI4E/AI4E/issues/30
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _udpClient.Close();
            }
        }
    }
}
