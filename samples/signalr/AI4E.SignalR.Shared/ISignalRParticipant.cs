using System.Threading.Tasks;

namespace AI4E.SignalR
{
    /// <summary>
    /// Represents a participant of the persistent connection infastructure.
    /// </summary>
    public interface ISignalRParticipant
    {
        Task DisconnectAsync(int seqNum, DisconnectReason reason);
        Task DisconnectedAsync(int seqNum);
        Task DeliverAsync(int seqNum, byte[] payload);
        Task CancelAsync(int seqNum, int corr);
        Task CancelledAsync(int seqNum, int corr);
    }
}
