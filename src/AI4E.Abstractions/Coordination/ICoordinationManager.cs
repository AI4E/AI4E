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

namespace AI4E.Coordination
{
    public interface ICoordinationManager
    {
        Task<IEntry> CreateAsync(string path, byte[] value, EntryCreationModes modes = default, CancellationToken cancellation = default);

        Task<IEntry> GetOrCreateAsync(string path, byte[] value, EntryCreationModes modes = default, CancellationToken cancellation = default);

        Task<IEntry> GetAsync(string path, CancellationToken cancellation = default);

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
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="path"/> or <paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="version"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled.</exception>
        Task<int> SetValueAsync(string path, byte[] value, int version = default, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously deleted the entry with the specified path.
        /// </summary>
        /// <param name="path">The key that identifies the value.</param>
        /// <param name="recursive">A boolean value specifying whether child entries shall be deleted recursively.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operatio or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asnychronous operation.
        /// When evaluated, the tasks result contains a version number that is equal to <paramref name="version"/> 
        /// if the operation suceeded 
        /// or the version of the current entry if <paramref name="version"/> is not equal to zero and the current entry's version is not equal to <paramref name="version"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="recursive"/> is false and the entry specified by <paramref name="path"/> contains child entries.</exception>
        Task<int> DeleteAsync(string path, int version = default, bool recursive = false, CancellationToken cancellation = default);

        Task<string> GetSessionAsync(CancellationToken cancellation = default);
    }

    [Flags]
    public enum EntryCreationModes
    {
        Default = 0,
        Ephemeral = 1
    }
}
