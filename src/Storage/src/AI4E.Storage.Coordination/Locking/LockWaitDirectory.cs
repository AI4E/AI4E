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

// TODO: Add unit-tests

using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Coordination.Session;
using AI4E.Utils;

namespace AI4E.Storage.Coordination.Locking
{
    /// <summary>
    /// A directory that can be used to wait for lock releases.
    /// </summary>
    public sealed class LockWaitDirectory : ILockWaitDirectory
    {
        private readonly AsyncWaitDirectory<(string key, SessionIdentifier session)> _readLockWaitDirectory;
        private readonly AsyncWaitDirectory<(string key, SessionIdentifier session)> _writeLockWaitDirectory;

        /// <summary>
        /// Creates a new instance of the <see cref="LockWaitDirectory"/> type.
        /// </summary>
        public LockWaitDirectory()
        {
            _readLockWaitDirectory = new AsyncWaitDirectory<(string key, SessionIdentifier session)>();
            _writeLockWaitDirectory = new AsyncWaitDirectory<(string key, SessionIdentifier session)>();
        }

        /// <inheritdoc/>
        public void NotifyReadLockRelease(
            string key,
            SessionIdentifier session)
        {
            _readLockWaitDirectory.Notify((key, session));
        }

        /// <inheritdoc/>
        public void NotifyWriteLockRelease(
            string key,
            SessionIdentifier session)
        {
            _writeLockWaitDirectory.Notify((key, session));
        }

        /// <inheritdoc/>
        public ValueTask WaitForReadLockNotificationAsync(
            string key,
            SessionIdentifier session,
            CancellationToken cancellation)
        {
            return _readLockWaitDirectory.WaitForNotificationAsync((key, session), cancellation).AsValueTask();
        }

        /// <inheritdoc/>
        public ValueTask WaitForWriteLockNotificationAsync(
            string key,
            SessionIdentifier session,
            CancellationToken cancellation)
        {
            return _writeLockWaitDirectory.WaitForNotificationAsync((key, session), cancellation).AsValueTask();
        }
    }
}
