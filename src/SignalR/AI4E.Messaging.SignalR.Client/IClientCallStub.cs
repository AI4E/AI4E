using System.Threading.Tasks;

namespace AI4E.Messaging.SignalR.Client
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
