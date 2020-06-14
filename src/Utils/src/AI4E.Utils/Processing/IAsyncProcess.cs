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

using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Processing
{
    /// <summary>
    /// Represents an asynchronous process.
    /// </summary>
    public interface IAsyncProcess
    {
        /// <summary>
        /// Gets the state of the process.
        /// </summary>
        AsyncProcessState State { get; }

        Task Startup { get; }
        Task Termination { get; }

        void Start();
        void Terminate();

        /// <summary>
        /// Asynchronously starts the process.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously terminates the process.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task TerminateAsync(CancellationToken cancellation = default);
    }

    /// <summary>
    /// Represents the state of an asynchronous process.
    /// </summary>
    public enum AsyncProcessState
    {
        /// <summary>
        /// The processis either in is initial state or terminated.
        /// </summary>
        Terminated = 0,

        /// <summary>
        /// The process is running currently.
        /// </summary>
        Running = 1,

        /// <summary>
        /// The process terminated with an exception. 
        /// Call <see cref=IAsyncProcess.TerminateAsync"/> to rethrow the exception.
        /// </summary>
        Failed = 2
    }
}
