using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Routing;
using Microsoft.AspNetCore.SignalR.Client;

namespace AI4E.SignalR.DotNetClient.Routing
{
    class FrontEndMessageDispatcher : IRemoteMessageDispatcher
    {
        public EndPointRoute LocalEndPoint => throw new NotImplementedException();
        private HubConnection _hubConnection;
        private int _nextSeqNum = 1;

        //TODO: make this private and create methods for getting and removing key/value pairs
        public ConcurrentDictionary<int, TaskCompletionSource<IDispatchResult>> _responseTable { get; set; }


        public FrontEndMessageDispatcher(HubConnection hubConnection)
        {
            _hubConnection = hubConnection;
        }

        public async Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, CancellationToken cancellation = default)
        {
            var seqNum = GetNextSeqNum();
            var tcs = new TaskCompletionSource<IDispatchResult>();
            while (!_responseTable.TryAdd(seqNum, tcs))
            {
                seqNum = GetNextSeqNum();
            }
            await _hubConnection.InvokeAsync("DispatchMessage", messageType, message, context, seqNum);
            return await tcs.Task;
        }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, EndPointRoute endPoint, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, EndPointRoute endPoint, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public Task<IDispatchResult> DispatchAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public Task<IDispatchResult> DispatchAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public Task<IDispatchResult> DispatchLocalAsync<TMessage>(TMessage message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public Task<IDispatchResult> DispatchLocalAsync(Type messageType, object message, DispatchValueDictionary context, bool publish, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public IHandlerRegistration<IMessageHandler<TMessage>> Register<TMessage>(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
        {
            throw new NotImplementedException();
        }

        public int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }
    }
}
