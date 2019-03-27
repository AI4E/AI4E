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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Coordination.Storage;

namespace AI4E.Coordination.Mocks
{
    public sealed class SessionStorageMock : ISessionStorage
    {
        private readonly object _mutex = new object();
        private readonly Dictionary<CoordinationSession, IStoredSession> _entries
            = new Dictionary<CoordinationSession, IStoredSession>();

        public SessionStorageMock()
        {

        }

        public Task<IStoredSession> GetSessionAsync(
            CoordinationSession session, CancellationToken cancellation = default)
        {
            bool found;
            IStoredSession result;

            lock (_mutex)
            {
                found = _entries.TryGetValue(session, out result);
            }

            if (!found)
            {
                result = null;
            }

            return Task.FromResult(result != null ? DeepCopy(result) : result);
        }

        public IAsyncEnumerable<IStoredSession> GetSessionsAsync(
            CancellationToken cancellation = default)
        {
            List<IStoredSession> result;

            lock (_mutex)
            {
                result = _entries.Values.ToList();
            }

            return result.Select(p => DeepCopy(p)).ToAsyncEnumerable();
        }

        public Task<IStoredSession> UpdateSessionAsync(
            IStoredSession value, IStoredSession comparand, CancellationToken cancellation = default)
        {
            bool success;
            CoordinationSession session;

            if (value == null && comparand == null)
            {
                throw new ArgumentException();
            }
            else if (comparand == null)
            {
                session = value.Session;
                lock (_mutex)
                {
                    success = _entries.TryAdd(value.Session, DeepCopy(value));
                }
            }
            else if (value == null)
            {
                session = comparand.Session;
                lock (_mutex)
                {
                    if (!_entries.TryGetValue(comparand.Session, out var entry))
                    {
                        success = false;
                    }

                    success = entry.StorageVersion == comparand.StorageVersion;

                    if (success)
                    {
                        _entries.Remove(comparand.Session);
                    }
                }
            }
            else if (value.Session != comparand.Session)
            {
                throw new ArgumentException($"The keys of {nameof(value)} and {nameof(comparand)} must be equal.");
            }
            else
            {
                session = comparand.Session;
                lock (_mutex)
                {
                    if (!_entries.TryGetValue(comparand.Session, out var entry))
                    {
                        success = false;
                    }

                    success = entry.StorageVersion == comparand.StorageVersion;

                    if (success)
                    {
                        _entries[comparand.Session] = DeepCopy(value);
                    }
                }
            }

            if (success)
            {
                return Task.FromResult(comparand);
            }

            return GetSessionAsync(session, cancellation);
        }

        private IStoredSession DeepCopy(IStoredSession entry)
        {
            return new StoredSessionMock
            {
                EntryPaths = entry.EntryPaths,
                IsEnded = entry.IsEnded,
                LeaseEnd = entry.LeaseEnd,
                Session = entry.Session,
                StorageVersion = entry.StorageVersion
            };
        }
    }
}
