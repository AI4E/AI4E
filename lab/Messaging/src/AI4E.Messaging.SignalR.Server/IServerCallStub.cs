using System;
using System.Threading.Tasks;

namespace AI4E.Messaging.SignalR.Server
{
    internal interface IServerCallStub
    {
        Task PushAsync(int seqNum, string endPoint, string securityToken, string payload);
        Task AckAsync(int seqNum);
        Task BadMessageAsync(int seqNum);

        Task<(string address, string endPoint, string securityToken, TimeSpan timeout)> ConnectAsync();
        Task<(string address, TimeSpan timeout)> ReconnectAsync(string endPoint, string securityToken, string previousAddress);
    }
}
