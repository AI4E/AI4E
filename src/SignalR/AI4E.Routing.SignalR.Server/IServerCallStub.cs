using System.Threading.Tasks;

namespace AI4E.Routing.SignalR.Server
{
    internal interface IServerCallStub
    {
        Task PushAsync(int seqNum, string endPoint, string securityToken, string payload);
        Task AckAsync(int seqNum);
        Task BadMessageAsync(int seqNum);

        Task<(string address, string endPoint, string securityToken)> ConnectAsync();
        Task<string> ReconnectAsync(string endPoint, string securityToken, string previousAddress);
    }
}
