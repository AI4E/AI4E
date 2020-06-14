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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Proxying
{
    internal interface IProxyInternal : IAsyncDisposable, IDisposable
    {
        int Id { get; }
        object LocalInstance { get; }
        Type RemoteType { get; }

        ActivationMode ActivationMode { get; }
        object[] ActivationParamers { get; }
        bool IsActivated { get; }

        ValueTask<Type> GetObjectTypeAsync(CancellationToken cancellation);

        void Activate(Type objectType);
        void Register(ProxyHost host, int proxyId, Action unregisterAction);

        Task<object> ExecuteAsync(MethodInfo method, object[] args);
    }
}

#nullable enable
