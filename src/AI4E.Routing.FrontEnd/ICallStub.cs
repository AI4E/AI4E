using System.Threading.Tasks;

namespace AI4E.Routing.FrontEnd
{
    internal interface ICallStub
    {
        Task DeliverAsync(int seqNum, byte[] bytes);
        Task AckAsync(int seqNum);
    }
}
