using System.Threading.Tasks;

namespace AI4E.Routing.SignalR.Client
{
    /*internal*/
    public interface ICallStub
    {
        Task DeliverAsync(int seqNum, string base64); // byte[] bytes);
        Task AckAsync(int seqNum);
    }

    /*internal*/
    public interface IServerCallStub : ICallStub
    {
        Task<string> InitAsync(string previousAddress);
    }
}
