using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Processing
{
    public interface ITriggerableAsyncProcess : IAsyncProcess
    {
        new TriggerableAsyncProcessState State { get; }
        void RegisterTrigger(ITrigger trigger);
        Task TriggerExecutionAsync(CancellationToken cancellation = default);
        void UnregisterTrigger(ITrigger trigger);
    }

    /// <summary>
    /// Represents the state of a triggerable process.
    /// </summary>
    public enum TriggerableAsyncProcessState
    {
        /// <summary>
        /// The process is in its initial state or terminated.
        /// </summary>
        Terminated = 0x00, // Static: Idle Dynamic: Terminated

        /// <summary>
        /// The process waits to be scheduled due to a trigger signal.
        /// </summary>
        WaitingForActivation = 0x01, // Static: Idle, Dynamic: Running

        /// <summary>
        /// The process terminated failing.
        /// </summary>
        Failed = 0x02, // Static: Idle, Dynamic: Failed

        /// <summary>
        /// The process is running once due to an external signal but is not beeing scheduled.
        /// </summary>
        RunningOnce = 0x10, // Static: Running, Dynamic: Terminated

        /// <summary>
        /// The process is running.
        /// </summary>
        Running = 0x11, // Static: Running, Dynamic: Running

        /// <summary>
        /// The process is currently running due to an external signal but its scheduled execution failed.
        /// </summary>
        RunningOnceFailed = 0x12, // Static: Running, Dynamic: Failed // TODO: Better name?
    }
}