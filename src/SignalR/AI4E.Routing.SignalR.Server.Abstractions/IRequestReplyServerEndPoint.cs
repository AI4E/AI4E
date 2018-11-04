using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.SignalR.Server
{
    public interface IRequestReplyServerEndPoint : IDisposable
    {
        Task<IRequestReplyServerReceiveResult> ReceiveAsync(CancellationToken cancellation = default);
        Task<IMessage> SendAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation = default);
    }

    public interface IRequestReplyServerReceiveResult : IMessageReceiveResult<Packet<EndPointAddress>>
    {
        EndPointAddress RemoteEndPoint { get; }
    }

    public static class MessageReceiveResultExtensions
    {
        public static async Task HandleAsync(
            this IRequestReplyServerReceiveResult messageReceiveResult,
            Func<IMessage, EndPointAddress, CancellationToken, Task<IMessage>> handler,
            CancellationToken cancellation)
        {
            if (messageReceiveResult == null)
                throw new ArgumentNullException(nameof(messageReceiveResult));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            using (var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, messageReceiveResult.Cancellation))
            {
                cancellation = combinedCancellationSource.Token;

                try
                {
                    var response = await handler(messageReceiveResult.Message, messageReceiveResult.RemoteEndPoint, cancellation);

                    if (response != null)
                    {
                        await messageReceiveResult.SendResponseAsync(response);
                    }
                    else
                    {
                        await messageReceiveResult.SendAckAsync();
                    }
                }
                catch (OperationCanceledException) when (messageReceiveResult.Cancellation.IsCancellationRequested)
                {
                    await messageReceiveResult.SendCancellationAsync();
                }
            }
        }

        public static async Task HandleAsync(
            this IRequestReplyServerReceiveResult messageReceiveResult,
            Func<IMessage, EndPointAddress, CancellationToken, Task> handler,
            CancellationToken cancellation)
        {
            if (messageReceiveResult == null)
                throw new ArgumentNullException(nameof(messageReceiveResult));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            using (var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, messageReceiveResult.Cancellation))
            {
                cancellation = combinedCancellationSource.Token;

                try
                {
                    await handler(messageReceiveResult.Message, messageReceiveResult.RemoteEndPoint, cancellation);
                    await messageReceiveResult.SendAckAsync();
                }
                catch (OperationCanceledException) when (messageReceiveResult.Cancellation.IsCancellationRequested)
                {
                    await messageReceiveResult.SendCancellationAsync();
                }
            }
        }
    }
}
