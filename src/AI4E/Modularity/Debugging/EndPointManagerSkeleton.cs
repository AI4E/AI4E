using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using Nito.AsyncEx;

namespace AI4E.Modularity.Debugging
{
    public sealed class EndPointManagerSkeleton : IAsyncDisposable
    {
        private readonly IEndPointManager _endPointManager;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly HashSet<EndPointRoute> _addedEndPoints = new HashSet<EndPointRoute>();
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly AsyncDisposeHelper _disposeHelper;

        public EndPointManagerSkeleton(IEndPointManager endPointManager, IMessageDispatcher messageDispatcher)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _endPointManager = endPointManager;
            _messageDispatcher = messageDispatcher;

            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #region Disposal

        private async Task DisposeInternalAsync()
        {
            Console.WriteLine("Disposing EndPointManagerSkeleton");

            using (await _lock.LockAsync())
            {
                foreach (var endPoint in _addedEndPoints)
                {
                    await _endPointManager.RemoveEndPointAsync(endPoint, cancellation: default);

                    await _messageDispatcher.DispatchAsync(new EndPointDisconnected(endPoint), publish: true);
                }
            }
        }

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        public Task Disposal => _disposeHelper.Disposal;

        #endregion

        public async Task AddEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                using (await _lock.LockAsync())
                {
                    if (_addedEndPoints.Contains(route))
                        return;

                    await _endPointManager.AddEndPointAsync(route, cancellation);
                    _addedEndPoints.Add(route);

                    await _messageDispatcher.DispatchAsync(new EndPointConnected(route), publish: true);
                }
            }
        }

        public async Task RemoveEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                using (await _lock.LockAsync())
                {
                    if (!_addedEndPoints.Contains(route))
                        return;

                    await _endPointManager.RemoveEndPointAsync(route, cancellation);
                    _addedEndPoints.Remove(route);

                    await _messageDispatcher.DispatchAsync(new EndPointDisconnected(route), publish: true);
                }
            }
        }

        public async Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                return await _endPointManager.ReceiveAsync(localEndPoint, cancellation);
            }
        }
        public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _endPointManager.SendAsync(message, remoteEndPoint, localEndPoint, cancellation);
            }
        }

        public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _endPointManager.SendAsync(response, request, cancellation);
            }
        }
    }
}
