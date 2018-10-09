using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing
{
    public interface IMessageReceiveResult : IDisposable
    {
        CancellationToken Cancellation { get; }
        IMessage Message { get; }

        // Send the specified response and end the request.
        Task SendResponseAsync(IMessage response);
        Task SendCancellationAsync();
        Task SendAckAsync();
    }

    public interface IMessageReceiveResult<TEndPointAddress> : IMessageReceiveResult, IDisposable
    {
        TEndPointAddress RemoteEndPoint { get; }
    }

    public interface IMessageReceiveResult<TAddress, TEndPointAddress> : IMessageReceiveResult<TEndPointAddress>, IMessageReceiveResult, IDisposable
    {
        TAddress RemoteAddress { get; }
    }

    public static class MessageReceiveResultExtensions
    {
        public static async Task HandleAsync(
            this IMessageReceiveResult messageReceiveResult,
            Func<IMessage, CancellationToken, Task<IMessage>> handler,
            CancellationToken cancellation)
        {
            if (messageReceiveResult == null)
                throw new ArgumentNullException(nameof(messageReceiveResult));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            try
            {
                var response = await handler(messageReceiveResult.Message, cancellation);

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

        public static async Task HandleAsync(
            this IMessageReceiveResult messageReceiveResult,
            Func<IMessage, CancellationToken, Task> handler,
            CancellationToken cancellation)
        {
            if (messageReceiveResult == null)
                throw new ArgumentNullException(nameof(messageReceiveResult));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            try
            {
                await handler(messageReceiveResult.Message, cancellation);
                await messageReceiveResult.SendAckAsync();
            }
            catch (OperationCanceledException) when (messageReceiveResult.Cancellation.IsCancellationRequested)
            {
                await messageReceiveResult.SendCancellationAsync();
            }
        }

        public static async Task HandleAsync<TEndPointAddress>(
            this IMessageReceiveResult<TEndPointAddress> messageReceiveResult,
            Func<IMessage, TEndPointAddress, CancellationToken, Task<IMessage>> handler,
            CancellationToken cancellation)
        {
            if (messageReceiveResult == null)
                throw new ArgumentNullException(nameof(messageReceiveResult));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

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

        public static async Task HandleAsync<TEndPointAddress>(
            this IMessageReceiveResult<TEndPointAddress> messageReceiveResult,
            Func<IMessage, TEndPointAddress, CancellationToken, Task> handler,
            CancellationToken cancellation)
        {
            if (messageReceiveResult == null)
                throw new ArgumentNullException(nameof(messageReceiveResult));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

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

        public static async Task HandleAsync<TAddress, TEndPointAddress>(
            this IMessageReceiveResult<TAddress, TEndPointAddress> messageReceiveResult,
            Func<IMessage, TAddress, TEndPointAddress, CancellationToken, Task<IMessage>> handler,
            CancellationToken cancellation)
        {
            if (messageReceiveResult == null)
                throw new ArgumentNullException(nameof(messageReceiveResult));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            try
            {
                var response = await handler(messageReceiveResult.Message, messageReceiveResult.RemoteAddress, messageReceiveResult.RemoteEndPoint, cancellation);

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

        public static async Task HandleAsync<TAddress, TEndPointAddress>(
            this IMessageReceiveResult<TAddress, TEndPointAddress> messageReceiveResult,
            Func<IMessage, TAddress, TEndPointAddress, CancellationToken, Task> handler,
            CancellationToken cancellation)
        {
            if (messageReceiveResult == null)
                throw new ArgumentNullException(nameof(messageReceiveResult));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            try
            {
                await handler(messageReceiveResult.Message, messageReceiveResult.RemoteAddress, messageReceiveResult.RemoteEndPoint, cancellation);
                await messageReceiveResult.SendAckAsync();
            }
            catch (OperationCanceledException) when (messageReceiveResult.Cancellation.IsCancellationRequested)
            {
                await messageReceiveResult.SendCancellationAsync();
            }
        }
    }
}
