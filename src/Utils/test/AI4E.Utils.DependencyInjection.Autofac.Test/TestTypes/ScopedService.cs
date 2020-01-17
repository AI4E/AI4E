using System;

namespace AI4E.Utils.DependencyInjection.Autofac.Test.TestTypes
{
    public sealed class ScopedService : IDisposable
    {
        public bool IsDisposed { get; private set; } = false;

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public sealed class ScopedService<T> : IDisposable
    {
        public bool IsDisposed { get; private set; } = false;

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
