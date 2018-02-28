using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AI4E.Modularity.RPC
{
    public sealed class Proxy<TRemote> : IProxy<TRemote>, IProxy
        where TRemote : class
    {
        private RPCHost _host;
        private int _id;
        private readonly Type _remoteType;

        public Proxy(TRemote instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            LocalInstance = instance;
        }

        internal Proxy(RPCHost host, int id, Type remoteType)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            _host = host;
            _id = id;
            _remoteType = remoteType;
        }

        public TRemote LocalInstance { get; }

        public Type ObjectType => LocalInstance?.GetType() ?? _remoteType;

        public int Id => _id;

        object IProxy.LocalInstance => LocalInstance;

        public void Dispose()
        {
            if (_host == null || LocalInstance != null)
            {
                return;
            }

            _host.Deactivate(_id, cancellation: default).GetAwaiter().GetResult(); // TODO
        }

        public Task ExecuteAsync(Expression<Action<TRemote>> expression)
        {
            if (LocalInstance != null)
            {
                // TODO
                throw new NotSupportedException();
            }

            return _host.SendMethodCallAsync<object>(expression.Body, Id, false);
        }

        public Task ExecuteAsync(Expression<Func<TRemote, Task>> expression)
        {
            if (LocalInstance != null)
            {
                // TODO
                throw new NotSupportedException();
            }

            return _host.SendMethodCallAsync<object>(expression.Body, Id, true);
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, TResult>> expression)
        {
            if (LocalInstance != null)
            {
                // TODO
                throw new NotSupportedException();
            }

            return _host.SendMethodCallAsync<TResult>(expression.Body, Id, false);
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, Task<TResult>>> expression)
        {
            if (LocalInstance != null)
            {
                // TODO
                throw new NotSupportedException();
            }

            return _host.SendMethodCallAsync<TResult>(expression.Body, Id, true);
        }

        public void Register(RPCHost host, int proxyId)
        {
            _host = host;
            _id = proxyId;
        }

        public static implicit operator Proxy<TRemote>(TRemote instance)
        {
            if (instance == null)
                return null;

            return new Proxy<TRemote>(instance);
        }
    }
}
