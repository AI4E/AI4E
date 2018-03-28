using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.RPC;

namespace AI4E.Modularity.Debugging
{
    public sealed class DebugEndPointManager : IEndPointManager
    {
        private IProxy<EndPointManagerSkeleton> _proxy;
        private readonly RPCHost _rpcHost;
        private readonly Task _initialization;

        public DebugEndPointManager(RPCHost rpcHost)
        {
            if (rpcHost == null)
                throw new ArgumentNullException(nameof(rpcHost));

            _rpcHost = rpcHost;
            _initialization = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _proxy = await _rpcHost.ActivateAsync<EndPointManagerSkeleton>(ActivationMode.Create, cancellation: default);
        }

        public async Task AddEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.AddEndPointAsync(route, cancellation));
        }

        public async Task RemoveEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.RemoveEndPointAsync(route, cancellation));
        }

        public async Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.ReceiveAsync(localEndPoint, CancellationToken.None));
        }

        public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.SendAsync(message, remoteEndPoint, localEndPoint, CancellationToken.None));
        }

        public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.SendAsync(response, request, CancellationToken.None));
        }

        public void Dispose() { }
    }
}
