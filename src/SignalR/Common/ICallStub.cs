using System.Threading.Tasks;

namespace AI4E.Routing.SignalR
{
    /*internal*/
    public interface ICallStub
    {
        Task DeliverAsync(int seqNum, byte[] bytes);
        Task AckAsync(int seqNum);
    }

    /*internal*/
    public interface IServerCallStub : ICallStub
    {
        Task<string> InitAsync(string previousAddress);
    }
}
