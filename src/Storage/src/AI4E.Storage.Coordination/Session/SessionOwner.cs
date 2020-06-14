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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Coordination.Session
{
    /// <summary>
    /// Represents the owner of a coordination service session.
    /// </summary>
    public sealed class SessionOwner : ISessionOwner
    {
        private readonly ISessionManager _sessionManager;
        private readonly ISessionIdentifierProvider _sessionProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<SessionOwner> _logger;

        private readonly CoordinationManagerOptions _options;
        private readonly AsyncProcess _updateSessionProcess;
        private readonly DisposableAsyncLazy<SessionIdentifier> _session;

        /// <summary>
        /// Creates a new instance of the <see cref="SessionOwner"/> type.
        /// </summary>
        /// <param name="sessionManager">A <see cref="ISessionManager"/> used to manage coordination service sessions.</param>
        /// <param name="sessionProvider">A <see cref="ISessionIdentifierProvider"/> used to create coordination service session identifiers.</param>
        /// <param name="dateTimeProvider">A <see cref="IDateTimeProvider"/> used to access the current date and time.</param>
        /// <param name="optionsAccessor">An <see cref="IOptions{TOptions}"/> accessor that is used to access the <see cref="CoordinationManagerOptions"/>.</param>
        /// <param name="logger">A <see cref="ILogger{TCategoryName}"/> used to for log operations or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="sessionProvider"/>, <paramref name="sessionProvider"/>,
        /// <paramref name="dateTimeProvider"/> or <paramref name="optionsAccessor"/> is <c>null</c>.
        /// </exception>
        public SessionOwner(ISessionManager sessionManager,
                                        ISessionIdentifierProvider sessionProvider,
                                        IDateTimeProvider dateTimeProvider,
                                        IOptions<CoordinationManagerOptions> optionsAccessor,
                                        ILogger<SessionOwner> logger = null)
        {
            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (sessionProvider == null)
                throw new ArgumentNullException(nameof(sessionProvider));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _sessionManager = sessionManager;
            _sessionProvider = sessionProvider;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;

            _options = optionsAccessor.Value ?? new CoordinationManagerOptions();
            _updateSessionProcess = new AsyncProcess(UpdateSessionProcess, start: false);
            _session = BuildSession();
            _session.Start();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _session.Dispose();
        }

        /// <inheritdoc/>
        public ValueTask<SessionIdentifier> GetSessionIdentifierAsync(CancellationToken cancellation)
        {
            return new ValueTask<SessionIdentifier>(_session.Task.WithCancellation(cancellation));
        }

        private DisposableAsyncLazy<SessionIdentifier> BuildSession()
        {
            return new DisposableAsyncLazy<SessionIdentifier>(
                factory: StartSessionAsync,
                disposal: TerminateSessionAsync,
                /*DisposableAsyncLazyOptions.Autostart |*/ DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        private async Task<SessionIdentifier> StartSessionAsync(CancellationToken cancellation)
        {
            var leaseLength = _options.LeaseLength;

            if (leaseLength <= TimeSpan.Zero)
            {
                leaseLength = CoordinationManagerOptions.LeaseLengthDefault;
                Assert(leaseLength > TimeSpan.Zero);
            }

            SessionIdentifier session;

            do
            {
                session = _sessionProvider.CreateUniqueSessionIdentifier();

                Assert(session != null);
            }
            while (!await _sessionManager.TryBeginSessionAsync(session, leaseEnd: _dateTimeProvider.GetCurrentTime() + leaseLength, cancellation));

            try
            {
                _logger?.LogInformation($"[{session}] Started session.");

                await _updateSessionProcess.StartAsync(cancellation);
            }
            catch (OperationCanceledException)
            {
                await _sessionManager.EndSessionAsync(session)
                                     .HandleExceptionsAsync(_logger);
                throw;
            }

            return session;
        }

        private async Task TerminateSessionAsync(SessionIdentifier session)
        {
            try
            {
                await _updateSessionProcess.TerminateAsync();
            }
            finally
            {
                Assert(session != null);
                await _sessionManager.EndSessionAsync(session)
                                     .HandleExceptionsAsync(_logger);
            }
        }

        private async Task UpdateSessionProcess(CancellationToken cancellation)
        {
            var session = await GetSessionIdentifierAsync(cancellation);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    await UpdateSessionAsync(session, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{session}] Failure while updating session {session}.");
                }
            }
        }

        private async Task UpdateSessionAsync(SessionIdentifier session, CancellationToken cancellation)
        {
            var leaseLength = _options.LeaseLength;

            if (leaseLength <= TimeSpan.Zero)
            {
                leaseLength = CoordinationManagerOptions.LeaseLengthDefault;
                Assert(leaseLength > TimeSpan.Zero);
            }

            var leaseLengthHalf = new TimeSpan(leaseLength.Ticks / 2);

            if (leaseLengthHalf <= TimeSpan.Zero)
            {
                leaseLengthHalf = new TimeSpan(1);
            }

            Assert(session != null);

            var leaseEnd = _dateTimeProvider.GetCurrentTime() + leaseLength;

            try
            {
                await _sessionManager.UpdateSessionAsync(session, leaseEnd, cancellation);

                await Task.Delay(leaseLengthHalf, cancellation);
            }
            catch (SessionTerminatedException)
            {
                Dispose();
            }
        }
    }
}
