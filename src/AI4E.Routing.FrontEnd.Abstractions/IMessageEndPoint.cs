using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Routing.FrontEnd
{
    public interface IMessageEndPoint<TMessage> 
        where TMessage : class
    {
        Task<TMessage> ReceiveAsync(CancellationToken cancellation);
        Task SendAsnyc(TMessage message, CancellationToken cancellation);
    }
}
