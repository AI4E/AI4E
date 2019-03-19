using System;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Mocks
{
    public class ServiceProviderMock : IServiceProvider
    {
        public ServiceProviderMock() { }

        public ServiceProviderMock(ServiceProviderMock parent)
        {
            Parent = parent;
        }

        public ServiceProviderMock Parent { get; }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
            {
                return new ServiceScopeFactoryMock(Parent ?? this);
            }

            return null;
        }
    }
}
