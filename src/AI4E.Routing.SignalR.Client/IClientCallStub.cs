using System.Threading.Tasks;

#if BLAZOR
namespace AI4E.Routing.Blazor
#else
namespace AI4E.Routing.SignalR.Client
#endif
{
    // TODO: This should be internal actually, but the signal r client proxy generator cannot handle this.
    public interface IClientCallStub
    {
        Task PushAsync(int seqNum, string payload);
        Task AckAsync(int seqNum);
        Task BadMessageAsync(int seqNum);
        Task BadClientAsync();
    }
}
