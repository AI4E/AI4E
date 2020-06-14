using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace AI4E.AspNetCore.Components.Build.Test.TestTypes
{
    public sealed class TestComponent : IComponent
    {
        public TestComponent(TestService testService)
        {
            TestService = testService;
        }

        public TestService TestService { get; }

        public void Attach(RenderHandle renderHandle) { }

        public Task SetParametersAsync(ParameterView parameters)
        {
            return Task.CompletedTask;
        }
    }
}
