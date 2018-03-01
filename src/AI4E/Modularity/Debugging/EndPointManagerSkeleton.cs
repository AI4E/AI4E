using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Debugging
{
    public sealed class EndPointManagerSkeleton : IDisposable
    {
        private readonly IEndPointManager _endPointManager;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly HashSet<EndPointRoute> _addedEndPoints = new HashSet<EndPointRoute>();
        private readonly object _lock = new object();
        private volatile bool _isDisposed;

        public EndPointManagerSkeleton(IEndPointManager endPointManager, IMessageDispatcher messageDispatcher)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _endPointManager = endPointManager;
            _messageDispatcher = messageDispatcher;
        }

        public void AddEndPoint(EndPointRoute route)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (_addedEndPoints.Contains(route))
                    return;

                _endPointManager.AddEndPoint(route);
                _addedEndPoints.Add(route);

                _messageDispatcher.DispatchAsync(new EndPointConnected(route), publish: true).GetAwaiter().GetResult();
            }
        }

        public void RemoveEndPoint(EndPointRoute route)
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (!_addedEndPoints.Contains(route))
                    return;

                _endPointManager.AddEndPoint(route);
                _addedEndPoints.Remove(route);

                _messageDispatcher.DispatchAsync(new EndPointDisconnected(route), publish: true).GetAwaiter().GetResult();
            }
        }

        public Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            return _endPointManager.ReceiveAsync(localEndPoint, cancellation);
        }

        public Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            return _endPointManager.SendAsync(message, remoteEndPoint, localEndPoint, cancellation);
        }

        public Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            return _endPointManager.SendAsync(response, request, cancellation);
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing EndPointManagerSkeleton");

            lock (_lock)
            {
                _isDisposed = true;
                foreach (var endPoint in _addedEndPoints)
                {
                    _endPointManager.RemoveEndPoint(endPoint);

                    _messageDispatcher.DispatchAsync(new EndPointDisconnected(endPoint), publish: true).GetAwaiter().GetResult();
                }
            }
        }
    }
}
