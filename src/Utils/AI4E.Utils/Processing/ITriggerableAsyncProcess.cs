/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

namespace AI4E.Utils.Processing
{
    /// <summary>
    /// Represents a triggerable async process.
    /// </summary>
    public interface ITriggerableAsyncProcess : IAsyncProcess
    {
        /// <summary>
        /// Gets the process state.
        /// </summary>
        new TriggerableAsyncProcessState State { get; }

        /// <summary>
        /// Registers a trigger.
        /// </summary>
        /// <param name="trigger">The trigger to register.</param>
        /// /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="trigger"/> is null.</exception>
        void RegisterTrigger(ITrigger trigger);

        /// <summary>
        /// Unregisters a trigger.
        /// </summary>
        /// <param name="trigger">The trigger to unregister.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="trigger"/> is null.</exception>
        void UnregisterTrigger(ITrigger trigger);

        /// <summary>
        /// Triggers the execution explicitely.
        /// </summary>
        void TriggerExecution();
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
