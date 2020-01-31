using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.Storage.Coordination
{
    public sealed class CoordinationSessionOwner : ICoordinationSessionOwner
    {
        private readonly ISessionManager _sessionManager;
        private readonly ISessionProvider _sessionProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<CoordinationSessionOwner> _logger;

        private readonly CoordinationManagerOptions _options;
        private readonly DisposableAsyncLazy<Session> _session;

        public CoordinationSessionOwner(ISessionManager sessionManager,
                                        ISessionProvider sessionProvider,
                                        IDateTimeProvider dateTimeProvider,
                                        IOptions<CoordinationManagerOptions> optionsAccessor,
                                        ILogger<CoordinationSessionOwner> logger = null)
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

            _session = BuildSession();
        }

        public void Dispose()
        {
            _session.Dispose();
        }

        public ValueTask<Session> GetSessionAsync(CancellationToken cancellation)
        {
            return new ValueTask<Session>(_session.Task.WithCancellation(cancellation));
        }

        private DisposableAsyncLazy<Session> BuildSession()
        {
            return new DisposableAsyncLazy<Session>(
                factory: StartSessionAsync,
                disposal: TerminateSessionAsync,
                DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        private async Task<Session> StartSessionAsync(CancellationToken cancellation)
        {
            var leaseLength = _options.LeaseLength;

            if (leaseLength <= TimeSpan.Zero)
            {
                leaseLength = CoordinationManagerOptions.LeaseLengthDefault;
                Debug.Assert(leaseLength > TimeSpan.Zero);
            }

            Session session;

            do
            {
                session = _sessionProvider.GetSession();

                Debug.Assert(session != null);
            }
            while (!await _sessionManager.TryBeginSessionAsync(session, leaseEnd: _dateTimeProvider.GetCurrentTime() + leaseLength, cancellation));

            _logger?.LogInformation($"[{session}] Started session.");

            return session;
        }

        private Task TerminateSessionAsync(Session session)
        {
            Debug.Assert(session != null);

            return _sessionManager.EndSessionAsync(session)
                                  .HandleExceptionsAsync(_logger);
        }
    }
}
