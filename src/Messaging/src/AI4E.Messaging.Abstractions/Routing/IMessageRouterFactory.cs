using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Messaging.Routing
{
    /// <summary>
    /// Represents a factory that can be used to create message routers.
    /// </summary>
    public interface IMessageRouterFactory
    {
        ValueTask<RouteEndPointAddress> GetDefaultEndPointAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously creates a message router for the default end point.
        /// </summary>
        /// <param name="routeMessageHandler">
        /// The message handler that is used to handle the messages routed to the caller.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the created message router.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="routeMessageHandler"/> is null.</exception>
        ValueTask<IMessageRouter> CreateMessageRouterAsync(
            IRouteMessageHandler routeMessageHandler,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously creates a message router for the specified end point.
        /// </summary>
        /// <param name="routeMessageHandler">
        /// The message handler that is used to handle the messages routed to the caller.
        /// </param>
        /// <param name="endPoint">The end point that shall be used.</param>
        /// <returns>
        ///  <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the created message router.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="routeMessageHandler"/> is null.</exception>
        ValueTask<IMessageRouter> CreateMessageRouterAsync(
            RouteEndPointAddress endPoint,
            IRouteMessageHandler routeMessageHandler,
            CancellationToken cancellation = default);
    }
}
