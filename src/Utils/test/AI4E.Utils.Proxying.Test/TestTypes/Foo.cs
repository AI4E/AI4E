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

using System;
using System.Threading.Tasks;

namespace AI4E.Utils.Proxying.Test.TestTypes
{
    public interface IFoo
    {
        bool IsDisposed { get; }

        int Add(int a, int b);
        Task<int> AddAsync(int a, int b);
        void Dispose();
        int Get();
        void Set(int i);
        Task SetAsync(int i);
        IProxy<Value> GetBackProxy(IProxy<Value> proxy);
        Task<int> ReadValueAsync(IProxy<Value> proxy);
    }

    public sealed class Foo : IDisposable, IFoo
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        public Task<int> AddAsync(int a, int b)
        {
            return Task.FromResult(a + b);
        }

        private int _i;

        public int Get()
        {
            return _i;
        }

        public void Set(int i)
        {
            _i = i;
        }

        public Task SetAsync(int i)
        {
            _i = i;
            return Task.CompletedTask;
        }

        public Task<int> ReadValueAsync(IValue transparentProxy)
        {
            return Task.FromResult(transparentProxy.GetValue());
        }

        public Task<int> ReadValueAsync(IProxy<Value> proxy)
        {
            return proxy.ExecuteAsync(value => value.GetValue());
        }

        public IProxy<Value> GetBackProxy(IProxy<Value> proxy)
        {
            return proxy;
        }

        public IValue GetBackTransparentProxy(IProxy<Value> proxy)
        {
            return proxy.Cast<IValue>().AsTransparentProxy();
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public bool IsDisposed { get; private set; }
    }
}
