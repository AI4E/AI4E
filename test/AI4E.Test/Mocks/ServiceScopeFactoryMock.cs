using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Mocks
{
    public class ServiceScopeFactoryMock : IServiceScopeFactory
    {
        public ServiceScopeFactoryMock(ServiceProviderMock serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public ServiceProviderMock ServiceProvider { get; }

        public IServiceScope CreateScope()
        {
            return new ServiceScopeMock(ServiceProvider);
        }
    }
}
