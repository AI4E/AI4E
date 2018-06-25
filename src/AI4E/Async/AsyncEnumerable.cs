using System;
using System.Collections.Generic;

namespace AI4E.Async
{
    public sealed class AsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly Func<IAsyncEnumerator<T>> _factory;

        public AsyncEnumerable(Func<IAsyncEnumerator<T>> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factory = factory;
        }

        public IAsyncEnumerator<T> GetEnumerator()
        {
            return _factory();
        }
    }
}
