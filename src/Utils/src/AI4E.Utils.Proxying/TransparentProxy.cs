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
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Proxying
{
    /// <summary>
    /// A base type for transparent proxies. This type is not meant to be used directly.
    /// </summary>
    /// <typeparam name="T">The type of dynamic proxy instance.</typeparam>
#pragma warning disable CA1063
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class TransparentProxy<T> : DispatchProxy, IProxyInternal
#pragma warning restore CA1063
            where T : class
    {
        internal IProxyInternal Proxy { get; private set; }

        #region IProxyInternal

        object IProxyInternal.LocalInstance => Proxy.LocalInstance;
        Type IProxyInternal.RemoteType => Proxy.RemoteType;
        int IProxyInternal.Id => Proxy.Id;

        ActivationMode IProxyInternal.ActivationMode => Proxy.ActivationMode;
        object[] IProxyInternal.ActivationParamers => Proxy.ActivationParamers;
        bool IProxyInternal.IsActivated => Proxy.IsActivated;

        void IProxyInternal.Activate(Type objectType)
        {
            Proxy.Activate(objectType);
        }

        ValueTask<Type> IProxyInternal.GetObjectTypeAsync(CancellationToken cancellation)
        {
            return Proxy.GetObjectTypeAsync(cancellation);
        }

        void IProxyInternal.Register(ProxyHost host, int proxyId, Action unregisterAction)
        {
            Proxy.Register(host, proxyId, unregisterAction);
        }

        Task<object> IProxyInternal.ExecuteAsync(MethodInfo method, object[] args)
        {
            return Proxy.ExecuteAsync(method, args);
        }

#pragma warning disable CA1063, CA1033, CA1816
        void IDisposable.Dispose()
#pragma warning restore CA1063, CA1033, CA1816
        {
            Proxy.Dispose();
        }
#pragma warning disable CA1063, CA1033, CA1816
        ValueTask IAsyncDisposable.DisposeAsync()
#pragma warning restore CA1063, CA1033, CA1816
        {
            return Proxy.DisposeAsync();
        }

        #endregion

        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));

            var task = Proxy.ExecuteAsync(targetMethod, args);

            if (targetMethod.ReturnType.IsTaskType(out var resultType))
            {
                // Convert the task to the correct type.
                return task.Convert(resultType);
            }
            else if (typeof(void).IsAssignableFrom(targetMethod.ReturnType))
            {
                // There is no result => Fire and forget
                return null;
            }
            else
            {
                // We have to wait for the task's result.
                return task.GetResultOrDefault();
            }
        }

        private void Configure(IProxyInternal proxy)
        {
            Proxy = proxy;
        }

        internal static T Create(IProxyInternal proxy)
        {
            object transparentProxy = Create<T, TransparentProxy<T>>();

            ((TransparentProxy<T>)transparentProxy).Configure(proxy);

            return (T)transparentProxy;
        }
    }
}

#nullable enable
