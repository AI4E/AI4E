using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

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
        private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();
        private readonly IAsyncProcess _receiveProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        public UdpEndPoint(ILogger<UdpEndPoint> logger)
        {
            _logger = logger;

            // We generate an IPv4 end-point for now.
            _udpClient = new UdpClient(port: 0);

            // TODO: Does this thing work in linux/unix too?
            // See: https://stackoverflow.com/questions/7201862/an-existing-connection-was-forcibly-closed-by-the-remote-host
            //uint IOC_IN = 0x80000000,
            //     IOC_VENDOR = 0x18000000,
            //     SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            //_udpClient.Client.IOControl(unchecked((int)SIO_UDP_CONNRESET), new byte[] { Convert.ToByte(false) }, null);

            LocalAddress = GetLocalAddress();

            if (LocalAddress == null)
            {
                throw new Exception("Cannot evaluate local address."); // TODO
            }

            _receiveProcess = new AsyncProcess(ReceiveProcedure);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        private IPEndPoint GetLocalAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return new IPEndPoint(ip, ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port);
                }
            }

            return null;
        }

        #region Initialization

        public Task Initialization => _initializationHelper.Initialization;

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _receiveProcess.StartAsync(cancellation);

            _logger?.LogDebug($"Started physical-end-point on local address '{LocalAddress}'.");
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            await _initializationHelper.CancelAsync().HandleExceptionsAsync();

            await _receiveProcess.TerminateAsync().HandleExceptionsAsync();
        }

        #endregion

        private async Task ReceiveProcedure(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Physical-end-point {LocalAddress}: Receive process started.");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    UdpReceiveResult receiveResult;
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

                    var message = new Message();

                    using (var memoryStream = new MemoryStream(receiveResult.Buffer))
                    {
                        await message.ReadAsync(memoryStream, cancellation);
                    }

                    await _rxQueue.EnqueueAsync(message, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exc) // TODO: This can end in an infinite loop, f.e. if the socket is down.
                {
                    _logger?.LogWarning(exc, $"Physical-end-point {LocalAddress}: Failure on receiving incoming message.");
                }
            }
        }

        public async Task SendAsync(IMessage message, IPEndPoint address, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            var buffer = new byte[message.Length];
            using (var memoryStream = new MemoryStream(buffer, writable: true))
            {
                await message.WriteAsync(memoryStream, cancellation);
            }

            await _udpClient.SendAsync(buffer, buffer.Length, address).WithCancellation(cancellation);
        }

        public Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            return _rxQueue.DequeueAsync(cancellation);
        }

        public IPEndPoint LocalAddress { get; }
    }
}
