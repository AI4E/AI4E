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
        Task SendResponseAsync(IMessage response, bool handled);
        Task SendCancellationAsync();
        Task SendAckAsync();
    }

    // TODO: Move this to a helper lib, as this should not be used for public facing APIs
    public interface IMessageReceiveResult<TPacket> : IMessageReceiveResult
        where TPacket : IPacket
    {
        TPacket Packet { get; }
    }

    // TODO: Move this to a helper lib, as this should not be used for public facing APIs
    public interface IPacket
    {
        IMessage Message { get; }

        IPacket WithMessage(IMessage message);
    }

    // TODO: Move this to a helper lib, as this should not be used for public facing APIs
    public interface IPacket<TPacket> : IPacket where TPacket : IPacket
    {
        new TPacket WithMessage(IMessage message);
    }

    // TODO: Move this to a helper lib, as this should not be used for public facing APIs
    public readonly struct Packet<TEndPointAddress> : IPacket<Packet<TEndPointAddress>>
    {
        public Packet(IMessage message, TEndPointAddress endPoint)
        {
            Message = message;
            EndPoint = endPoint;
        }

        public IMessage Message { get; }
        public TEndPointAddress EndPoint { get; }

        public Packet<TEndPointAddress> WithMessage(IMessage message)
        {
            return new Packet<TEndPointAddress>(message, EndPoint);
        }

        IPacket IPacket.WithMessage(IMessage message)
        {
            return WithMessage(message);
        }
    }

    // TODO: Move this to a helper lib, as this should not be used for public facing APIs
    public readonly struct Packet<TEndPointAddress, TAddress> : IPacket<Packet<TEndPointAddress, TAddress>>
    {
        public Packet(IMessage message, TEndPointAddress endPoint, TAddress address)
        {
            Message = message;
            EndPoint = endPoint;
            Address = address;
        }

        public IMessage Message { get; }
        public TEndPointAddress EndPoint { get; }
        public TAddress Address { get; }

        // TODO: Move this to a helper lib, as this should not be used for public facing APIs
        public Packet<TEndPointAddress, TAddress> WithMessage(IMessage message)
        {
            return new Packet<TEndPointAddress, TAddress>(message, EndPoint, Address);
        }

        IPacket IPacket.WithMessage(IMessage message)
        {
            return WithMessage(message);
        }
    }

    public static partial class MessageReceiveResultExtensions
    {
        public static async Task HandleAsync(
            this IMessageReceiveResult messageReceiveResult,
            Func<IMessage, CancellationToken, Task<(IMessage message, bool handled)>> handler,
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
                    var (response, handled) = await handler(messageReceiveResult.Message, cancellation);

                    if (response != null)
                    {
                        await messageReceiveResult.SendResponseAsync(response, handled);
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
            this IMessageReceiveResult messageReceiveResult,
            Func<IMessage, CancellationToken, Task<IMessage>> handler,
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
            using (var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, messageReceiveResult.Cancellation))
            {
                cancellation = combinedCancellationSource.Token;

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
        }
    }
}
