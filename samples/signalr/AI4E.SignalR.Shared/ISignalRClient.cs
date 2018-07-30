using System.Threading.Tasks;

namespace AI4E.SignalR
{
    /// <summary>
    /// Represents the client side of the persistent connection infastructure.
    /// </summary>
    public interface ISignalRClient : ISignalRParticipant
    {
        Task AcceptAsync(int corr, string securityToken);
        Task RejectAsync(int corr, RejectReason reason);
    }
}
