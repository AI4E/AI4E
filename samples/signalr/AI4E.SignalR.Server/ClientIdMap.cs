using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.SignalR.Server
{
    // TODO: This should be a wrapper around the coordination service to allow the messaging solution to be used in clusters.
    public sealed class ActiveClientSet : IActiveClientSet
    {
        private static readonly TimeSpan _sessionDuration = TimeSpan.FromMinutes(5); // TODO: This should be configurable

        private volatile ImmutableDictionary<string, (string securityToken, DateTime sessionEnd)> _storage;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ISecurityTokenGenerator _securityTokenGenerator;

        public ActiveClientSet(IDateTimeProvider dateTimeProvider, ISecurityTokenGenerator securityTokenGenerator)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (securityTokenGenerator == null)
                throw new ArgumentNullException(nameof(securityTokenGenerator));

            _storage = ImmutableDictionary<string, (string clientId, DateTime sessionEnd)>.Empty;
            _dateTimeProvider = dateTimeProvider;
            _securityTokenGenerator = securityTokenGenerator;
        }

        /// <summary>
        /// Asynchronously adds the specified client to the set and associates a security token.
        /// </summary>
        /// <param name="clientId">The if of the client that shall be added.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains the security token associated with <paramref name="clientId"/> 
        /// or null if a client with <paramref name="clientId"/> is already present.
        /// </returns>
        public ValueTask<string> AddAsync(string clientId, CancellationToken cancellation)
        {
            if (clientId == null)
                throw new ArgumentNullException(nameof(clientId));

            var now = _dateTimeProvider.GetCurrentTime();

            ImmutableDictionary<string, (string securityToken, DateTime sessionEnd)> current = _storage, // Volatile read op
                                                                                     start,
                                                                                     desired;

            string securityToken;

            do
            {
                start = current;

                // An entry is already present and the session is not terminated.
                if (start.TryGetValue(clientId, out var entry) && now <= entry.sessionEnd)
                {
                    return new ValueTask<string>(result: null);
                }

                securityToken = _securityTokenGenerator.GenerateSecurityToken(clientId, now);
                Assert(securityToken != null);
                desired = start.SetItem(clientId, (securityToken, now + _sessionDuration));
                current = Interlocked.CompareExchange(ref _storage, desired, start);
            }
            while (start != current);

            return new ValueTask<string>(securityToken);
        }

        /// <summary>
        /// Asynchronously tries to remove a client from the set.
        /// </summary>
        /// <param name="clientId">The client id thats shall be removed.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the client was found and removed.
        /// </returns>
        public Task<bool> RemoveAsync(string clientId, CancellationToken cancellation)
        {
            if (clientId == null)
                throw new ArgumentNullException(nameof(clientId));

            var now = _dateTimeProvider.GetCurrentTime();

            ImmutableDictionary<string, (string clientId, DateTime sessionEnd)> current = _storage, // Volatile read op
                                                                                start,
                                                                                desired;

            do
            {
                start = current;

                // There is no entry present or its session is terminated.
                if (!start.TryGetValue(clientId, out var entry) || now > entry.sessionEnd)
                {
                    return Task.FromResult(false);
                }

                desired = start.Remove(clientId);
                current = Interlocked.CompareExchange(ref _storage, desired, start);
            }
            while (start != current);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Asynchronously retrieves the security token associated with the specified client id.
        /// </summary>
        /// <param name="clientId">The client id thats security token shall be retrieved.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains the security token associated with <paramref name="clientId"/> 
        /// or null if no entry for <paramref name="clientId"/> can be found.
        /// </returns>
        public ValueTask<string> GetSecurityTokenAsync(string clientId, CancellationToken cancellation)
        {
            if (clientId == null)
                throw new ArgumentNullException(nameof(clientId));

            var now = _dateTimeProvider.GetCurrentTime();

            ImmutableDictionary<string, (string clientId, DateTime sessionEnd)> current = _storage, // Volatile read op
                                                                                start,
                                                                                desired;

            (string securityToken, DateTime sessionEnd) entry;

            do
            {
                start = current;

                // There is no entry present or its session is terminated.
                if (!start.TryGetValue(clientId, out entry) || now > entry.sessionEnd)
                {
                    return new ValueTask<string>(result: null);
                }

                desired = start.SetItem(clientId, (entry.securityToken, now + _sessionDuration));
                current = Interlocked.CompareExchange(ref _storage, desired, start);
            }
            while (start != current);

            return new ValueTask<string>(entry.securityToken);
        }
    }
}
