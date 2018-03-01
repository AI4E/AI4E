using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AI4E.Modularity.RPC
{
    public sealed class Proxy<TRemote> : IProxy<TRemote>, IProxy
        where TRemote : class
    {
        private RPCHost _host;
        private int _id;
        private Action _unregisterAction;
        private readonly Type _remoteType;
        private readonly bool _ownsInstance;

        public Proxy(TRemote instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            LocalInstance = instance;
        }

        public Proxy(TRemote instance, bool ownsInstance) : this(instance)
        {
            _ownsInstance = ownsInstance;
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

        public Type ObjectType => IsRemoteProxy ? _remoteType : LocalInstance.GetType();

        public int Id => _id;

        object IProxy.LocalInstance => LocalInstance;

        private bool IsRemoteProxy => LocalInstance == null;

        public void Dispose()
        {
            if (IsRemoteProxy)
            {
                Debug.Assert(_host != null);

                _host.Deactivate(_id, cancellation: default).GetAwaiter().GetResult(); // TODO
            }
            else
            {
                _unregisterAction();

                if (_ownsInstance && LocalInstance is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        public Task ExecuteAsync(Expression<Action<TRemote>> expression)
        {
            if (!IsRemoteProxy)
            {
                // TODO
                throw new NotSupportedException();
            }

            return _host.SendMethodCallAsync<object>(expression.Body, Id, false);
        }

        public Task ExecuteAsync(Expression<Func<TRemote, Task>> expression)
        {
            if (!IsRemoteProxy)
            {
                // TODO
                throw new NotSupportedException();
            }

            return _host.SendMethodCallAsync<object>(expression.Body, Id, true);
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, TResult>> expression)
        {
            if (!IsRemoteProxy)
            {
                // TODO
                throw new NotSupportedException();
            }

            return _host.SendMethodCallAsync<TResult>(expression.Body, Id, false);
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, Task<TResult>>> expression)
        {
            if (!IsRemoteProxy)
            {
                // TODO
                throw new NotSupportedException();
            }

            return _host.SendMethodCallAsync<TResult>(expression.Body, Id, true);
        }

        public void Register(RPCHost host, int proxyId, Action unregisterAction)
        {
            if (unregisterAction == null)
                throw new ArgumentNullException(nameof(unregisterAction));

            _host = host;
            _id = proxyId;
            _unregisterAction = unregisterAction;
        }

        public static implicit operator Proxy<TRemote>(TRemote instance)
        {
            if (instance == null)
                return null;

            return new Proxy<TRemote>(instance);
        }

        public Proxy<T> Cast<T>()
            where T : class
        {
            if (!typeof(T).IsAssignableFrom(ObjectType))
            {
                throw new InvalidCastException();
            }

            if (IsRemoteProxy)
            {
                return new Proxy<T>(_host, Id, ObjectType);
            }
            else
            {
                return new Proxy<T>((T)(object)LocalInstance, _ownsInstance) { _host = _host, _id = _id, _unregisterAction = _unregisterAction };
            }
        }
    }
}
