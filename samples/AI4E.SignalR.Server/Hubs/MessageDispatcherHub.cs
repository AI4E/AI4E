using AI4E.SignalR.Server.Abstractions;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using AI4E.Routing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
using AI4E.SignalR.DotNetClient.Api;

namespace AI4E.SignalR.Server.Hubs
{
    public class MessageDispatcherHub : Hub
    {
        private readonly IClientRemoteMessageDispatcherAssociationStorage _clientRemoteMessageDispatcherAssociationStorage;
        private readonly IEndPointManager _endPointManager;
        private readonly IRouteStore _routeStore;
        private readonly IMessageTypeConversion _messageTypeConversion;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteMessageDispatcher> _logger;

        public MessageDispatcherHub(IClientRemoteMessageDispatcherAssociationStorage clientRemoteMessageDispatcherAssociationStorage, 
                                    IEndPointManager endPointManager, IRouteStore routeStore, 
                                    IMessageTypeConversion messageTypeConversion, 
                                    IServiceProvider serviceProvider, ILogger<RemoteMessageDispatcher> logger)
        {
            _clientRemoteMessageDispatcherAssociationStorage = clientRemoteMessageDispatcherAssociationStorage;
            _endPointManager = endPointManager;
            _routeStore = routeStore;
            _messageTypeConversion = messageTypeConversion;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async override Task OnConnectedAsync()
        {
            _clientRemoteMessageDispatcherAssociationStorage.AddAssociation(Context.ConnectionId, BuildRemoteMessageDispatcher);
            await base.OnConnectedAsync();
        }

        private IRemoteMessageDispatcher BuildRemoteMessageDispatcher(string connectionId)
        {
            var lep = _endPointManager.GetLogicalEndPoint(EndPointRoute.CreateRoute(connectionId));
            return new RemoteMessageDispatcher(lep, _routeStore, _messageTypeConversion, _serviceProvider, _logger);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _clientRemoteMessageDispatcherAssociationStorage.RemoveAssociation(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task DispatchMessage(TestSignalRCommand message, DispatchValueDictionary context, int seqNum)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var dispatcher = _clientRemoteMessageDispatcherAssociationStorage.GetMessageDispatcher(Context.ConnectionId);
            var dispatchResult = await dispatcher.DispatchAsync(message);
            //if(dispatchResult.IsSuccess)
            //{
                await Clients.Caller.SendAsync("GetDispatchResult", seqNum, dispatchResult);
            //}
        }
    }
}
