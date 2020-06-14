using System;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging.Mocks
{
    public class ServiceScopeMock : IServiceScope
    {
        public ServiceScopeMock(ServiceProviderMock unscopedServiceProvider)
        {
            UnscopedServiceProvider = unscopedServiceProvider;
            ServiceProvider = new ServiceProviderMock(UnscopedServiceProvider);
        }

        public IServiceProvider ServiceProvider { get; }

        public ServiceProviderMock UnscopedServiceProvider { get; }

        public void Dispose() { }
    }
}
