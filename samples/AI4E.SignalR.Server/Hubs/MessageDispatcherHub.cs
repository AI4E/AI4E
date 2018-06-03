using AI4E.SignalR.Server.Abstractions;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using AI4E.Routing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;

namespace AI4E.SignalR.Server.Hubs
{
    public class MessageDispatcherHub : Hub
    {
        private readonly IClientLogicalEndPointAssociationStorage _clientLogicalEndPointAssociationStorage;
        private readonly IClientRemoteMessageDispatcherAssociationStorage _clientRemoteMessageDispatcherAssociationStorage;
        private readonly IEndPointManager _endPointManager;
        private readonly IRouteStore _routeStore;
        private readonly IMessageTypeConversion _messageTypeConversion;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RemoteMessageDispatcher> _logger;

        public MessageDispatcherHub(IClientLogicalEndPointAssociationStorage clientLogicalEndPointAssociationStorage, 
                                    IClientRemoteMessageDispatcherAssociationStorage clientRemoteMessageDispatcherAssociationStorage, 
                                    IEndPointManager endPointManager, IRouteStore routeStore, 
                                    IMessageTypeConversion messageTypeConversion, 
                                    IServiceProvider serviceProvider, ILogger<RemoteMessageDispatcher> logger)
        {
            _clientLogicalEndPointAssociationStorage = clientLogicalEndPointAssociationStorage;
            _clientRemoteMessageDispatcherAssociationStorage = clientRemoteMessageDispatcherAssociationStorage;
            _endPointManager = endPointManager;
            _routeStore = routeStore;
            _messageTypeConversion = messageTypeConversion;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async override Task OnConnectedAsync()
        {
            var lep = _endPointManager.GetLogicalEndPoint(EndPointRoute.CreateRoute(Context.ConnectionId));
            await _clientLogicalEndPointAssociationStorage.AddAssociationAsync(Context.ConnectionId, lep);
            await _clientRemoteMessageDispatcherAssociationStorage.AddAssociationAsync(Context.ConnectionId, new RemoteMessageDispatcher(lep, _routeStore, _messageTypeConversion, _serviceProvider, _logger));
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await _clientLogicalEndPointAssociationStorage.RemoveAssociationAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task DispatchMessage(Type messageType, object message, DispatchValueDictionary context, int seqNum)
        {
            if (messageType == null)
            {
                throw new ArgumentNullException(nameof(messageType));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var dispatcher = await _clientRemoteMessageDispatcherAssociationStorage.GetMessageDispatcherAsync(Context.ConnectionId);
            var dispatchResult = await dispatcher.DispatchAsync(message);
            if(dispatchResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("GetDispatchResult", seqNum, dispatchResult);
            }
        }
    }
}
