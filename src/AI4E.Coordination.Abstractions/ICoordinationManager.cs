/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;

namespace AI4E.Coordination
{
    /// <summary>
    /// Represents a coordination service.
    /// </summary>
    public interface ICoordinationManager : IDisposable
    {
        /// <summary>
        /// Asynchronously creates a coordination entry with the specified path.
        /// </summary>
        /// <param name="path">The path of the coordination entry.</param>
        /// <param name="value">The value of the coordination entry.</param>
        /// <param name="modes">The creation mode to use.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operatio or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the created entry.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="modes"/> is not a combination of the values defined in <see cref="EntryCreationModes"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="DuplicateEntryException">Thrown if the coordination service contains an entry with the specified path.</exception>
        /// <exception cref="SessionTerminatedException">Thrown if the session is terminated.</exception>
        ValueTask<IEntry> CreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes = default, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously creates a coordination entry with the specified path if no entry with the path does already exist, or returns the existing entry.
        /// </summary>
        /// <param name="path">The path of the coordination entry.</param>
        /// <param name="value">The value of the coordionation entry.</param>
        /// <param name="modes">The creation mode to use.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operatio or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the coordination entry.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="modes"/> is not a combination of the values defined in <see cref="EntryCreationModes"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="SessionTerminatedException">Thrown if the session is terminated.</exception>
        ValueTask<IEntry> GetOrCreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes = default, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously returns the entry with the specified path.
        /// </summary>
        /// <param name="path">The path of the coordination entry.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operatio or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the coordination entry or null if no matching entry exists.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="SessionTerminatedException">Thrown if the session is terminated.</exception>
        ValueTask<IEntry> GetAsync(CoordinationEntryPath path, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously sets the value with the specified path.
        /// </summary>
        /// <param name="path">The key that identifies the value.</param>
        /// <param name="value">The value that shall be set.</param>
        /// <param name="version">The version the existing value must have in order to execute the operation or zero to execute the operation anyway.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operatio or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the version of the entry before it was modified or zero if <paramref name="version"/> is zero.
        /// If the value is equal to <paramref name="version"/> the operation was executed, otherwise a version conflict occured.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="version"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="SessionTerminatedException">Thrown if the session is terminated.</exception>
        ValueTask<int> SetValueAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, int version = default, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously deleted the entry with the specified path.
        /// </summary>
        /// <param name="path">The key that identifies the value.</param>
        /// <param name="recursive">A boolean value specifying whether child entries shall be deleted recursively.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operatio or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains a version number that is equal to <paramref name="version"/> 
        /// if the operation suceeded 
        /// or the version of the current entry if <paramref name="version"/> is not equal to zero and the current entry's version is not equal to <paramref name="version"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="recursive"/> is false and the entry specified by <paramref name="path"/> contains child entries.</exception>
        /// <exception cref="SessionTerminatedException">Thrown if the session is terminated.</exception>
        ValueTask<int> DeleteAsync(CoordinationEntryPath path, int version = default, bool recursive = false, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously retrieves the current session.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operatio or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the current session.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        ValueTask<Session> GetSessionAsync(CancellationToken cancellation = default);
    }

    /// <summary>
    /// Represents a factory for coordination managers.
    /// </summary>
    public interface ICoordinationManagerFactory
    {
        /// <summary>
        /// Creates a new coordination manager.
        /// </summary>
        /// <returns>The created coordination manager.</returns>
        ICoordinationManager CreateCoordinationManager();
    }

    /// <summary>
    /// Defines creation modes for coordination entries.
    /// </summary>
    [Flags]
    public enum EntryCreationModes
    {
        /// <summary>
        /// The default creation mode.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Specifies that a coordination entry is ephemeral.
        /// </summary>
        /// <remarks>
        /// The coordination entry's lifetime is bound to the lifetime of the session in that's scope the entry is created.
        /// Ephemeral nodes cannot have child nodes.
        /// </remarks>
        Ephemeral = 1
    }
}
