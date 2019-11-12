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
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Proxying
{
    internal sealed class CastProxy<TRemote, TCast> : IProxy<TCast>
        where TRemote : class
        where TCast : class
    {
        public CastProxy(Proxy<TRemote> original)
        {
            Original = original;
        }

        public TCast LocalInstance => Original.IsRemoteProxy ? null : (TCast)(object)Original.LocalInstance;

        object IProxy.LocalInstance => LocalInstance;

        public ValueTask<Type> GetObjectTypeAsync(CancellationToken cancellation)
        {
            return Original.GetObjectTypeAsync(cancellation);
        }

        public Type RemoteType => typeof(TCast);

        public int Id => Original.Id;

        internal Proxy<TRemote> Original { get; }

        private Expression<TDelegate> ConvertExpression<TDelegate>(LambdaExpression expression)
            where TDelegate : Delegate
        {
            var parameter = expression.Parameters.First();
            var body = expression.Body;

            var newParameter = Expression.Parameter(typeof(TRemote));
            var newBody = ParameterExpressionReplacer.ReplaceParameter(body, parameter, newParameter);
            return Expression.Lambda<TDelegate>(newBody, newParameter);
        }

        public Task ExecuteAsync(Expression<Action<TCast>> expression)
        {
            return Original.ExecuteAsync(ConvertExpression<Action<TRemote>>(expression));
        }

        public Task ExecuteAsync(Expression<Func<TCast, Task>> expression)
        {
            return Original.ExecuteAsync(ConvertExpression<Func<TRemote, Task>>(expression));
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression<Func<TCast, TResult>> expression)
        {
            return Original.ExecuteAsync(ConvertExpression<Func<TRemote, TResult>>(expression));
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression<Func<TCast, Task<TResult>>> expression)
        {
            return Original.ExecuteAsync(ConvertExpression<Func<TRemote, Task<TResult>>>(expression));
        }

        public IProxy<T> Cast<T>() where T : class
        {
            return Original.Cast<T>();
        }

        public void Dispose()
        {
            Original.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return Original.DisposeAsync();
        }

        public Task Disposal => Original.Disposal;

        public TCast AsTransparentProxy()
        {
            return Original.AsTransparentProxy<TCast>();
        }
    }
}

#nullable enable
