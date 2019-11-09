/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

#nullable disable

using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;

namespace AI4E.Utils.Proxying
{
    internal sealed class Proxy<TRemote> : IProxy<TRemote>, IProxyInternal
        where TRemote : class
    {
        static Proxy()
        {
            var remoteType = typeof(TRemote);
            ProxyHost.AddLoadedRemoteType(remoteType);
        }

        private ProxyHost _host;
        private Action _unregisterAction;
        private Type _objectType;
        private readonly bool _ownsInstance;
        private readonly AsyncDisposeHelper _disposeHelper;

        private bool _isActivated;
        private TaskCompletionSource<Type> _objectTypeTaskCompletionSource;

        #region C'tor

        internal Proxy(TRemote instance, bool ownsInstance)
        {
            LocalInstance = instance;

            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            _ownsInstance = ownsInstance;
        }

        internal Proxy(ProxyHost host, int id, Type objectType)
        {
            _host = host;
            Id = id;
            _objectType = objectType;
            _isActivated = true;
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        internal Proxy(ProxyHost proxyHost, int id, ActivationMode activationMode, object[] activationParameters)
        {
            _host = proxyHost;
            Id = id;
            ActivationMode = activationMode;
            ActivationParamers = activationParameters;
            _isActivated = false;
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        public TRemote LocalInstance { get; }
        object IProxyInternal.LocalInstance => LocalInstance;
        object IProxy.LocalInstance => LocalInstance;
        internal bool IsRemoteProxy => LocalInstance == null;
        public Type RemoteType => typeof(TRemote);
        public int Id { get; private set; }

        public ActivationMode ActivationMode { get; }
        public object[] ActivationParamers { get; }
        public bool IsActivated => Volatile.Read(ref _isActivated);

        public ValueTask<Type> GetObjectTypeAsync(CancellationToken cancellation)
        {
            if (!IsRemoteProxy)
            {
                return new ValueTask<Type>(LocalInstance.GetType());
            }

            if (Volatile.Read(ref _isActivated))
            {
                return new ValueTask<Type>(_objectType);
            }

            var objectTypeTaskCompletionSource = GetObjectTypeTaskCompletionSource();

            var task = objectTypeTaskCompletionSource.Task.WithCancellation(cancellation);
            return new ValueTask<Type>(task);

        }

        private TaskCompletionSource<Type> GetObjectTypeTaskCompletionSource()
        {
            var objectTypeTaskCompletionSource = Volatile.Read(ref _objectTypeTaskCompletionSource);

            if (objectTypeTaskCompletionSource == null)
            {
                objectTypeTaskCompletionSource = new TaskCompletionSource<Type>();

                var existing = Interlocked.CompareExchange(ref _objectTypeTaskCompletionSource, objectTypeTaskCompletionSource, null);

                if (existing != null)
                {
                    objectTypeTaskCompletionSource = existing;
                }
            }

            return objectTypeTaskCompletionSource;
        }

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            if (IsRemoteProxy)
            {
                Debug.Assert(_host != null);

                await _host.Deactivate(Id, cancellation: default).ConfigureAwait(false);
            }
            else
            {
                _unregisterAction?.Invoke();

                if (_ownsInstance)
                {
                    if (LocalInstance is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (LocalInstance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        ~Proxy()
        {
            try
            {
                // TODO: Why is _disposeHelper null sometimes?
                _disposeHelper?.Dispose();
            }
            catch (ObjectDisposedException) { }
        }

        #endregion

        public async Task ExecuteAsync(Expression<Action<TRemote>> expression)
        {
            try
            {
                using var guard = _disposeHelper.GuardDisposal(cancellation: default);
                if (IsRemoteProxy)
                {
                    await _host.SendMethodCallAsync<object>(expression.Body, this, false).ConfigureAwait(false);
                }
                else
                {
                    var compiled = expression.Compile();

                    compiled.Invoke(LocalInstance);
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task ExecuteAsync(Expression<Func<TRemote, Task>> expression)
        {
            try
            {
                using var guard = _disposeHelper.GuardDisposal(cancellation: default);
                if (IsRemoteProxy)
                {
                    await _host.SendMethodCallAsync<object>(expression.Body, this, true).ConfigureAwait(false);
                    return;
                }

                var compiled = expression.Compile();

                await compiled.Invoke(LocalInstance).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, TResult>> expression)
        {
            try
            {
                using var guard = _disposeHelper.GuardDisposal(cancellation: default);
                if (IsRemoteProxy)
                {
                    return await _host.SendMethodCallAsync<TResult>(expression.Body, this, false).ConfigureAwait(false);
                }

                var compiled = expression.Compile();
                return compiled.Invoke(LocalInstance);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, Task<TResult>>> expression)
        {
            try
            {
                using var guard = _disposeHelper.GuardDisposal(cancellation: default);
                if (IsRemoteProxy)
                {
                    return await _host.SendMethodCallAsync<TResult>(expression.Body, this, true).ConfigureAwait(false);
                }

                var compiled = expression.Compile();

                return await compiled.Invoke(LocalInstance).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task<object> ExecuteAsync(MethodInfo method, object[] args)
        {
            Debug.Assert(IsRemoteProxy);

            try
            {
                using var guard = _disposeHelper.GuardDisposal(cancellation: default);
                return await _host.SendMethodCallAsync<object>(
                    method, args, this, typeof(Task).IsAssignableFrom(method.ReturnType))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public void Activate(Type objectType)
        {
            _objectType = objectType;
            Volatile.Write(ref _isActivated, true);

            var objectTypeTaskCompletionSource = GetObjectTypeTaskCompletionSource();
            objectTypeTaskCompletionSource.TrySetResult(objectType);
        }

        public void Register(ProxyHost host, int proxyId, Action unregisterAction)
        {
            if (unregisterAction == null)
                throw new ArgumentNullException(nameof(unregisterAction));

            _host = host;
            Id = proxyId;
            _unregisterAction = unregisterAction;
        }

        public IProxy<TCast> Cast<TCast>()
            where TCast : class
        {
            if (!typeof(TCast).IsAssignableFrom(RemoteType))
                throw new ArgumentException($"Unable to cast the proxy. The type {RemoteType} cannot be cast to type {typeof(TCast)}.");

            return new CastProxy<TRemote, TCast>(this);
        }

        public TRemote AsTransparentProxy()
        {
            return AsTransparentProxy<TRemote>();
        }

        public TCast AsTransparentProxy<TCast>()
            where TCast : class
        {
            if (!typeof(TCast).IsInterface)
                throw new NotSupportedException("The proxy type must be an interface.");

            var result = TransparentProxy<TCast>.Create(this);
            var type = result.GetType();
            ProxyHost.AddTransparentProxyType(type);

            return result;
        }
    }
}

#nullable enable
