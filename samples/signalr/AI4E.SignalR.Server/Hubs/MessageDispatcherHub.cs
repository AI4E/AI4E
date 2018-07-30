using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.SignalR.Server.Hubs
{
    public sealed class MessageDispatcherHub : Hub<ISignalRClient>, ISignalRServer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IActiveClientSet _activeClients;
        private readonly ILogger<MessageDispatcherHub> _logger;

        private static readonly ConcurrentDictionary<int, CancellationTokenSource> _cancellationRegistry = new ConcurrentDictionary<int, CancellationTokenSource>(); // TODO

        public MessageDispatcherHub(IServiceProvider serviceProvider, IActiveClientSet activeClients, ILogger<MessageDispatcherHub> logger = null)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (activeClients == null)
                throw new ArgumentNullException(nameof(activeClients));

            _serviceProvider = serviceProvider;
            _activeClients = activeClients;
            _logger = logger;
            EnsureMessageHandlerRegistered(serviceProvider);
        }

        #region Workaround for #9

        private static volatile bool _handled = false;
        private static readonly object _lock = new object();

        internal static void EnsureMessageHandlerRegistered(IServiceProvider serviceProvider)
        {
            if (_handled)
                return;

            lock (_lock)
            {
                if (_handled)
                    return;

                // Load the message dispatcher from the service container to ensure 
                // that it gets constructed and all message handlers are registered.
                serviceProvider.GetRequiredService<IMessageDispatcher>();

                // Wait sume time in order for all handlers to be registered
                Thread.Sleep(1000);

                _handled = true;
            }
        }

        #endregion

        public async Task ConnectAsync(int seqNum, string clientId)
        {
            CancellationTokenSource cancellationSource;

            try
            {
                cancellationSource = _cancellationRegistry.GetOrAdd(seqNum, _ => new CancellationTokenSource());
                var securityToken = await _activeClients.AddAsync(clientId, cancellationSource.Token);

                if (securityToken == null)
                {
                    await Clients.Caller.RejectAsync(seqNum, RejectReason.IdAlreadyAssigned);
                }
                else
                {
                    // TODO: Allocate the vep

                    await Clients.Caller.AcceptAsync(seqNum, securityToken);
                }
            }
            catch (OperationCanceledException)
            {
                await Clients.Caller.RejectAsync(seqNum, RejectReason.Canceled);
            }
            finally
            {
                _cancellationRegistry.TryRemove(seqNum, out _);
            }
        }

        public async Task ReconnectAsync(int seqNum, string clientId, string securityToken)
        {
            CancellationTokenSource cancellationSource;

            try
            {
                cancellationSource = _cancellationRegistry.GetOrAdd(seqNum, _ => new CancellationTokenSource());
                var expectedSecurityToken = await _activeClients.GetSecurityTokenAsync(clientId, cancellationSource.Token);

                if (expectedSecurityToken != securityToken)
                {
                    await Clients.Caller.RejectAsync(seqNum, RejectReason.BadClient);
                }
                else
                {
                    // TODO: Allocate the vep if not yet present.

                    await Clients.Caller.AcceptAsync(seqNum, expectedSecurityToken);
                }
            }
            catch (OperationCanceledException)
            {
                await Clients.Caller.RejectAsync(seqNum, RejectReason.Canceled);
            }
            finally
            {
                _cancellationRegistry.TryRemove(seqNum, out _);
            }
        }

        public Task DisconnectAsync(int seqNum, DisconnectReason reason)
        {
            throw new System.NotImplementedException();
        }

        public Task DisconnectedAsync(int seqNum)
        {
            throw new System.NotImplementedException();
        }

        public Task DeliverAsync(int seqNum, byte[] payload)
        {
            throw new System.NotImplementedException();
        }

        public Task CancelAsync(int seqNum, int corr)
        {
            throw new System.NotImplementedException();
        }

        public Task CancelledAsync(int seqNum, int corr)
        {
            throw new NotImplementedException();
        }
    }
}
