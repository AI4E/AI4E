using System.Threading.Tasks;

#if BLAZOR
namespace AI4E.Routing.Blazor
#else
namespace AI4E.Routing.SignalR.Client
#endif
{
    internal interface IClientCallStub
    {
        Task PushAsync(int seqNum, string payload);
        Task AckAsync(int seqNum);
        Task BadMessageAsync(int seqNum);
        Task BadClientAsync();
    }
}
