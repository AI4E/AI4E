using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;


namespace AI4E.Coordination
{
    public sealed partial class CoordinationManager<TAddress>
    {
        private sealed class SessionManagement : IDisposable
        {
            private readonly ICoordinationManager _coordinationManager;
            private readonly ISessionManager _sessionManager;
            private readonly IDateTimeProvider _dateTimeProvider;
            private readonly ILogger _logger;

            private readonly IAsyncProcess _updateSessionProcess;
            private readonly IAsyncProcess _sessionCleanupProcess;

            public SessionManagement(ICoordinationManager coordinationManager,
                                     ISessionManager sessionManager,
                                     IDateTimeProvider dateTimeProvider,
                                     ILogger logger = null)
            {
                if (coordinationManager == null)
                    throw new ArgumentNullException(nameof(coordinationManager));

                if (sessionManager == null)
                    throw new ArgumentNullException(nameof(sessionManager));

                if (dateTimeProvider == null)
                    throw new ArgumentNullException(nameof(dateTimeProvider));

                _coordinationManager = coordinationManager;
                _sessionManager = sessionManager;
                _dateTimeProvider = dateTimeProvider;
                _logger = logger;

                _updateSessionProcess = new AsyncProcess(UpdateSessionProcess);
                _sessionCleanupProcess = new AsyncProcess(SessionCleanupProcess);

                _updateSessionProcess.Start();
                _sessionCleanupProcess.Start();
            }


            private async Task SessionCleanupProcess(CancellationToken cancellation)
            {
                var session = await _coordinationManager.GetSessionAsync(cancellation);

                _logger?.LogTrace($"[{session}] Started session cleanup process.");

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        var terminated = await _sessionManager.WaitForTerminationAsync(cancellation);

                        Assert(terminated != null);

                        // Our session is terminated or
                        // There are no session in the session manager. => Our session must be terminated.
                        if (terminated == session)
                        {
                            Dispose();
                        }
                        else
                        {
                            await CleanupSessionAsync(terminated, cancellation);
                        }
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                    catch (Exception exc)
                    {
                        _logger?.LogWarning(exc, $"[{session}] Failure while cleaning up terminated sessions.");
                    }
                }
            }

            private async Task UpdateSessionProcess(CancellationToken cancellation)
            {
                var session = await _coordinationManager.GetSessionAsync(cancellation);

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

            private async Task UpdateSessionAsync(Session session, CancellationToken cancellation)
            {
                Assert(session != null);

                var leaseEnd = _dateTimeProvider.GetCurrentTime() + _leaseLength;

                try
                {
                    await _sessionManager.UpdateSessionAsync(session, leaseEnd, cancellation);

                    await Task.Delay(_leaseLengthHalf);
                }
                catch (SessionTerminatedException)
                {
                    Dispose();
                }
            }

            private async Task CleanupSessionAsync(Session session, CancellationToken cancellation)
            {
                _logger?.LogInformation($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Cleaning up session '{session}'.");

                var entries = await _sessionManager.GetEntriesAsync(session, cancellation);

                await Task.WhenAll(entries.Select(async entry =>
                {
                    await _coordinationManager.DeleteAsync(entry, version: default, recursive: false, cancellation);
                    await _sessionManager.RemoveSessionEntryAsync(session, entry, cancellation);
                }));

                await _sessionManager.EndSessionAsync(session, cancellation);
            }

            public void Dispose()
            {
                _updateSessionProcess.Terminate();
                _sessionCleanupProcess.Terminate();
            }
        }
    }
}
