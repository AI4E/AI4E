using System;

namespace AI4E.Internal
{
    // Null object for the IDisposable interface
    internal sealed class NoOpDisposable : IDisposable
    {
        public static NoOpDisposable Instance { get; } = new NoOpDisposable();

        private NoOpDisposable() { }

        void IDisposable.Dispose() { }
    }
}
