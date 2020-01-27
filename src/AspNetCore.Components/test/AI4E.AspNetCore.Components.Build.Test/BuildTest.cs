using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Build.Test.Utils;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI4E.AspNetCore.Components.Build.Test
{
    public class BuildTest : IDisposable
    {
        private readonly AssemblyLoadContext _assemblyLoadContext;
        private readonly string _workingDirectory;

        public BuildTest()
        {
            _workingDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AI4E.AspNetCore.Components.Build.Test.TestTypes");
            _assemblyLoadContext = new TestAssemblyLoadContext(_workingDirectory);

        }

        public void Dispose()
        {
            _assemblyLoadContext.Unload();
        }

        [Fact]
        public async Task Test()
        {
            using (_assemblyLoadContext.EnterContextualReflection())
            {
                var handler = new ReplaceComponentFactoryHandler
                {
                    DllFilePath = Path.Combine(_workingDirectory, "Microsoft.AspNetCore.Components.dll")
                };

                var success = await handler.ExecuteAsync();

                Assert.True(success);

                var componentsAssembly = _assemblyLoadContext.LoadFromAssemblyName(new AssemblyName("Microsoft.AspNetCore.Components"));
                var componentFactoryAssembly = _assemblyLoadContext.LoadFromAssemblyName(new AssemblyName("AI4E.AspNetCore.Components.Factory"));
                var testTypesAssembly = _assemblyLoadContext.LoadFromAssemblyName(new AssemblyName("AI4E.AspNetCore.Components.Build.Test.TestTypes"));

                var componentType = componentsAssembly.GetType("Microsoft.AspNetCore.Components.IComponent");
                var componentFactoryType = componentsAssembly.GetType("Microsoft.AspNetCore.Components.ComponentFactory");
                var testComponentType = testTypesAssembly.GetType("AI4E.AspNetCore.Components.Build.Test.TestTypes.TestComponent");
                var testComponentActivatorType = testTypesAssembly.GetType("AI4E.AspNetCore.Components.Build.Test.TestTypes.TestComponentActivator");
                var componentActivatorType = componentFactoryAssembly.GetType("AI4E.AspNetCore.Components.Factory.IComponentActivator");
                var testServiceType = testTypesAssembly.GetType("AI4E.AspNetCore.Components.Build.Test.TestTypes.TestService");
                var testService = Activator.CreateInstance(testServiceType);

                var services = new ServiceCollection();
                services.AddSingleton(componentActivatorType, testComponentActivatorType);
                services.AddSingleton(testServiceType, testService);
                var serviceProvider = services.BuildServiceProvider();

                var componentFactory = Activator.CreateInstance(componentFactoryType);
                var instantiateComponentMethod = componentFactory.GetType().GetMethod("InstantiateComponent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.DefaultBinder, new[] { typeof(IServiceProvider), typeof(Type) }, modifiers: null);

                var component = instantiateComponentMethod.Invoke(componentFactory, new object[] { serviceProvider, testComponentType });

                Assert.NotNull(component);
                Assert.IsAssignableFrom(componentType, component);
                Assert.IsType(testComponentType, component);
                Assert.Same(testService, testComponentType.GetProperty("TestService").GetValue(component));
            }
        }
    }
}
