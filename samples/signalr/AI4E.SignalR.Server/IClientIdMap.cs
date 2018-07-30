using System.Threading;
using System.Threading.Tasks;

namespace AI4E.SignalR.Server
{
    /// <summary>
    /// Represents a set of active clients.
    /// </summary>
    public interface IActiveClientSet
    {
        /// <summary>
        /// Asynchronously adds the specified client to the set and associates a security token.
        /// </summary>
        /// <param name="clientId">The if of the client that shall be added.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains the security token associated with <paramref name="clientId"/> 
        /// or null if a client with <paramref name="clientId"/> is already present.
        /// </returns>
        ValueTask<string> AddAsync(string clientId, CancellationToken cancellation);

        /// <summary>
        /// Asynchronously tries to remove a client from the set.
        /// </summary>
        /// <param name="clientId">The client id thats shall be removed.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the client was found and removed.
        /// </returns>
        Task<bool> RemoveAsync(string clientId, CancellationToken cancellation);

        /// <summary>
        /// Asynchronously retrieves the security token associated with the specified client id.
        /// </summary>
        /// <param name="clientId">The client id thats security token shall be retrieved.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A value task that represents the asynchronous operation.
        /// When evaluated, the tasks result contains the security token associated with <paramref name="clientId"/> 
        /// or null if no entry for <paramref name="clientId"/> can be found.
        /// </returns>
        ValueTask<string> GetSecurityTokenAsync(string clientId, CancellationToken cancellation);
    }
}
