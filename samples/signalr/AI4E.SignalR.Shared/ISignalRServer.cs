using System.Threading.Tasks;

namespace AI4E.SignalR
{
    /// <summary>
    /// Represents the client side of the persistent connection infastructure.
    /// </summary>
    public interface ISignalRServer : ISignalRParticipant
    {
        Task ConnectAsync(int seqNum, string clientId);
        Task ReconnectAsync(int seqNum, string clientId, string securityToken);
    }
}
