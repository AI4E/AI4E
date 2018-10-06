using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.SignalR.Server
{
    public interface ILogicalServerEndPoint
    {
        Task<IMessage> SendAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation = default);
        Task<IMessageReceiveResult<EndPointAddress>> ReceiveAsync(CancellationToken cancellation = default);
    }
}
