using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    public static class DomainServiceResolver
    {
        private sealed class DomainServiceResolverState : IDisposable
        {
            public static DomainServiceResolverState Default = new DomainServiceResolverState(GetEmptyServiceProvider(), null);

            private static IServiceProvider GetEmptyServiceProvider()
            {
                var services = new ServiceCollection();
                return services.BuildServiceProvider();
            }

            private readonly DomainServiceResolverState _previous;
            private bool _isDisposed;

            public DomainServiceResolverState(IServiceProvider domainServices, DomainServiceResolverState previous)
            {
                if (domainServices is null)
                    throw new ArgumentNullException(nameof(domainServices));

                DomainServices = domainServices;
                _previous = previous;
            }

            public IServiceProvider DomainServices { get; }

            public void Dispose()
            {
                if (_isDisposed)
                    return;

                if (_current.Value == this)
                {
                    SetCurrentInstance(GetPreviousToEnable());
                }

                _isDisposed = true;
            }

            private DomainServiceResolverState GetPreviousToEnable()
            {
                for (var c = _previous; c != null; c = c._previous)
                {
                    if (!c._isDisposed)
                        return c;
                }

                return null;
            }
        }

        public static IDisposable UseDomainServices(IServiceProvider domainServices)
        {
            var serviceResolver = new DomainServiceResolverState(domainServices, _current.Value);
            SetCurrentInstance(serviceResolver);

            return serviceResolver;
        }

        private static readonly AsyncLocal<DomainServiceResolverState> _current = new AsyncLocal<DomainServiceResolverState>();

        public static IServiceProvider DomainServices => (_current.Value ?? DomainServiceResolverState.Default).DomainServices;

        private static void SetCurrentInstance(DomainServiceResolverState domainServiceResolver)
        {
            _current.Value = domainServiceResolver;
        }
    }
}
