using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E
{
    public sealed class AsyncProvider<T> : IAsyncProvider<T>
    {
        private readonly Func<CancellationToken, Task<T>> _factory;

        public AsyncProvider(Func<CancellationToken, Task<T>> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factory = factory;
        }

        public Task<T> ProvideInstanceAsync(CancellationToken cancellation = default)
        {
            return _factory(cancellation);
        }

        public static AsyncProvider<T> FromValue(T value)
        {
            return new AsyncProvider<T>(_ => Task.FromResult(value));
        }

        public static implicit operator AsyncProvider<T>(Func<CancellationToken, Task<T>> factory)
        {
            return new AsyncProvider<T>(factory);
        }

        public static implicit operator AsyncProvider<T>(Func<Task<T>> factory)
        {
            return new AsyncProvider<T>(_ => factory());
        }

        public static implicit operator AsyncProvider<T>(Func<T> factory)
        {
            return new AsyncProvider<T>(_ => Task.FromResult(factory()));
        }
    }

    public sealed class AsyncProvider
    {
        public static IAsyncProvider<T> Create<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return AsyncProvider<T>.FromValue(value);
        }

        public static IAsyncProvider<T> Create<T>(Func<T> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return (AsyncProvider<T>)factory;
        }

        public static IAsyncProvider<T> Create<T>(Func<Task<T>> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return (AsyncProvider<T>)factory;
        }

        public static IAsyncProvider<T> Create<T>(Func<CancellationToken, Task<T>> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            return (AsyncProvider<T>)factory;
        }
    }
}
