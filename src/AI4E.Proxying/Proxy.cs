/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        Proxy.cs
 * Types:           AI4E.Proxying.Proxy'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AI4E.Async;

namespace AI4E.Proxying
{
    public sealed class Proxy<TRemote> : IProxy<TRemote>, IProxy
        where TRemote : class
    {
        private ProxyHost _host;
        private Action _unregisterAction;
        private readonly Type _remoteType;
        private readonly bool _ownsInstance;
        private readonly AsyncDisposeHelper2 _disposeHelper;

        #region C'tor

        public Proxy(TRemote instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            LocalInstance = instance;

            _disposeHelper = new AsyncDisposeHelper2(DisposeInternalAsync);
        }

        public Proxy(TRemote instance, bool ownsInstance) : this(instance)
        {
            _ownsInstance = ownsInstance;
        }

        internal Proxy(ProxyHost host, int id, Type remoteType)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            _host = host;
            Id = id;
            _remoteType = remoteType;
            _disposeHelper = new AsyncDisposeHelper2(DisposeInternalAsync);
        }

        #endregion

        public TRemote LocalInstance { get; }

        public Type ObjectType => IsRemoteProxy ? _remoteType : LocalInstance.GetType();

        public int Id { get; private set; }

        object IProxy.LocalInstance => LocalInstance;

        private bool IsRemoteProxy => LocalInstance == null;

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            if (IsRemoteProxy)
            {
                Debug.Assert(_host != null);

                await _host.Deactivate(Id, cancellation: default);
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
                _disposeHelper.Dispose();
            }
            catch (ObjectDisposedException) { }
        }

        #endregion

        public async Task ExecuteAsync(Expression<Action<TRemote>> expression)
        {
            try
            {
                using (var guard = _disposeHelper.GuardDisposal(cancellation: default))
                {
                    if (IsRemoteProxy)
                    {
                        await _host.SendMethodCallAsync<object>(expression.Body, Id, false);
                    }

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
                using (var guard = _disposeHelper.GuardDisposal(cancellation: default))
                {
                    if (IsRemoteProxy)
                    {
                        await _host.SendMethodCallAsync<object>(expression.Body, Id, true);
                        return;
                    }

                    var compiled = expression.Compile();

                    await compiled.Invoke(LocalInstance);
                }
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
                using (var guard = _disposeHelper.GuardDisposal(cancellation: default))
                {
                    if (IsRemoteProxy)
                    {
                        return await _host.SendMethodCallAsync<TResult>(expression.Body, Id, false);
                    }

                    var compiled = expression.Compile();
                    return compiled.Invoke(LocalInstance);
                }
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
                using (var guard = _disposeHelper.GuardDisposal(cancellation: default))
                {
                    if (IsRemoteProxy)
                    {
                        return await _host.SendMethodCallAsync<TResult>(expression.Body, Id, true);
                    }

                    var compiled = expression.Compile();

                    return await compiled.Invoke(LocalInstance);
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public void Register(ProxyHost host, int proxyId, Action unregisterAction)
        {
            if (unregisterAction == null)
                throw new ArgumentNullException(nameof(unregisterAction));

            _host = host;
            Id = proxyId;
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

            // TODO: Should we also copy over the dispose-helper?

            if (IsRemoteProxy)
            {
                return new Proxy<T>(_host, Id, ObjectType);
            }
            else
            {
                return new Proxy<T>((T)(object)LocalInstance, _ownsInstance) { _host = _host, Id = Id, _unregisterAction = _unregisterAction };
            }
        }
    }
}
