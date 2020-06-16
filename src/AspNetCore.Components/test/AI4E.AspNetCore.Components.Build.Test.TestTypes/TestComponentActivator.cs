using System;
using AI4E.AspNetCore.Components.Factory;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore.Components.Build.Test.TestTypes
{
    public sealed class TestComponentActivator : IComponentActivator
    {
        private readonly IServiceProvider _serviceProvider;

        public TestComponentActivator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IComponent ActivateComponent(Type componentType)
        {
            return (IComponent)ActivatorUtilities.CreateInstance(_serviceProvider, componentType);
        }
    }
}
