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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Storage;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination.Storage
{
    /// <summary>
    /// Represents the storage for session entries.
    /// </summary>
    public sealed class SessionStorage : ISessionStorage
    {
        private readonly IDatabase _database;

        /// <summary>
        /// Creates a new instance of the <see cref="SessionStorage"/> type.
        /// </summary>
        /// <param name="database">The underlying database that is used to store session entries.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="database"/> is null.</exception>
        public SessionStorage(IDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        #region Session

        /// <inheritdoc />
        public async Task<IStoredSession> GetSessionAsync(SessionIdentifier session, CancellationToken cancellation)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var compactStringSession = session.ToString();

            var storedSession = await _database.GetOneAsync<StoredSession>(p => p.Id == compactStringSession, cancellation);

            Assert(storedSession == null || (storedSession as IStoredSession).Session == session);

            return storedSession;
        }

        /// <inheritdoc />
        public IAsyncEnumerable<IStoredSession> GetSessionsAsync(CancellationToken cancellation)
        {
            return _database.GetAsync<StoredSession>(cancellation);
        }

        /// <inheritdoc />
        public async Task<IStoredSession> UpdateSessionAsync(IStoredSession value, IStoredSession comparand, CancellationToken cancellation)
        {
            var convertedValue = ConvertValue(value);
            var convertedComparand = ConvertValue(comparand);

            if (convertedValue != null && convertedComparand != null && convertedValue.Id != convertedComparand.Id)
            {
                throw new ArgumentException($"The keys of {nameof(value)} and {nameof(comparand)} must be equal.");
            }

            if (await _database.CompareExchangeAsync(convertedValue, convertedComparand, (left, right) => left.StorageVersion == right.StorageVersion, cancellation))
            {
                return comparand;
            }

            return await GetSessionAsync((comparand ?? value).Session, cancellation);
        }

        private StoredSession ConvertValue(IStoredSession value)
        {
            StoredSession convertedValue = null;

            if (value != null)
            {
                convertedValue = value as StoredSession ?? new StoredSession(value);

                Assert(convertedValue != null);
                Assert((convertedValue as IStoredSession).Session == value.Session);
            }

            return convertedValue;
        }

        #endregion

        private sealed class StoredSession : IStoredSession
        {
            public StoredSession() { }

            public StoredSession(IStoredSession session)
            {
                Id = session.Session.ToString();
                IsEnded = session.IsEnded;
                LeaseEnd = session.LeaseEnd;
                EntryPaths = GetEntryPaths(session);
                StorageVersion = session.StorageVersion;
            }

            private static string[] GetEntryPaths(IStoredSession session)
            {
                if (session.EntryPaths.IsDefaultOrEmpty)
                    return Array.Empty<string>();

                return session.EntryPaths.Select(p => p.EscapedPath.ConvertToString()).ToArray();
            }

            public string Id { get; set; }
            SessionIdentifier IStoredSession.Session => SessionIdentifier.FromChars(Id.AsSpan());
            public bool IsEnded { get; set; }
            public DateTime LeaseEnd { get; set; }
            public string[] EntryPaths { get; set; }
            public int StorageVersion { get; set; }

            ImmutableArray<CoordinationEntryPath> IStoredSession.EntryPaths => EntryPaths.Select(p => CoordinationEntryPath.FromEscapedPath(p.AsMemory())).ToImmutableArray();
        }
    }
}
