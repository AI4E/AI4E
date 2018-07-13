using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

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

        public AsyncEnumerable(Func<CancellationToken, IAsyncEnumerator<T>> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factory = () => new CancellableAsyncEnumerator(factory);
        }

        public IAsyncEnumerator<T> GetEnumerator()
        {
            return _factory();
        }

        private sealed class CancellableAsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly Func<CancellationToken, IAsyncEnumerator<T>> _factory;
            private IAsyncEnumerator<T> _enumerator;

            public CancellableAsyncEnumerator(Func<CancellationToken, IAsyncEnumerator<T>> factory)
            {
                Assert(factory != null);
                _factory = factory;
            }

            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (_enumerator == null)
                {
                    _enumerator = _factory(cancellationToken);

                    if (_enumerator == null)
                        throw new InvalidOperationException();
                }

                return _enumerator.MoveNext(cancellationToken);
            }

            public T Current
            {
                get
                {
                    if (_enumerator == null)
                    {
                        return default;
                    }

                    return _enumerator.Current;
                }
            }

            public void Dispose()
            {
                _enumerator?.Dispose();
            }
        }
    }
}
