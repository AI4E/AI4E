using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.RPC;

namespace AI4E.Modularity.Debugging
{
    public sealed class DebugEndPointManager : IEndPointManager
    {
        private readonly IProxy<IEndPointManager> _proxy;

        public DebugEndPointManager(IProxy<IEndPointManager> proxy)
        {
            if (proxy == null)
                throw new ArgumentNullException(nameof(proxy));

            _proxy = proxy;
        }

        public void AddEndPoint(EndPointRoute route)
        {
            _proxy.ExecuteAsync(p => p.AddEndPoint(route)).GetAwaiter().GetResult(); // TODO
        }

        public Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            return _proxy.ExecuteAsync(p => p.ReceiveAsync(localEndPoint, CancellationToken.None));
        }

        public void RemoveEndPoint(EndPointRoute route)
        {
            _proxy.ExecuteAsync(p => p.RemoveEndPoint(route)).GetAwaiter().GetResult(); // TODO
        }

        public Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            return _proxy.ExecuteAsync(p => p.SendAsync(message, remoteEndPoint, localEndPoint, CancellationToken.None));
        }

        public Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            return _proxy.ExecuteAsync(p => p.SendAsync(response, request, CancellationToken.None));
        }

        public void Dispose() { }
    }
}
