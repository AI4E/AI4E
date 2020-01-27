using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

#if SUPPORTS_AMBIENT_ASSEMBLY_LOAD_CONTEXT
using System.Runtime.Loader;
#endif
namespace AI4E.AspNetCore.Components.Factory
{
    public static class ComponentFactoryLoader
    {
        private static readonly Assembly _componentsFactoryAssembly = LoadComponentsFactoryAssembly();
        private static readonly Type _componentActivatorType = GetComponentActivatorType();
        private static readonly Type _defaultComponentActivatorType = GetDefaultComponentActivatorType();
        private static readonly Func<object, Type, object> _componentActivator = BuildComponentActivator();
        private static readonly ConditionalWeakTable<IServiceProvider, object> _defaultComponentActivators
            = new ConditionalWeakTable<IServiceProvider, object>();
        private static readonly ConditionalWeakTable<IServiceProvider, object>.CreateValueCallback _buildDefaultComponentActivator
           = BuildDefaultComponentActivator;

        private static Assembly LoadComponentsFactoryAssembly()
        {
            var assemblyName = "AI4E.AspNetCore.Components.Factory";
#if SUPPORTS_AMBIENT_ASSEMBLY_LOAD_CONTEXT
            var currentLoadContext = AssemblyLoadContext.CurrentContextualReflectionContext;

            if (currentLoadContext != null)
            {
                return currentLoadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));
            }
#endif
            return Assembly.Load(assemblyName);
        }

        private static Type GetComponentActivatorType()
        {
            return _componentsFactoryAssembly.GetType("AI4E.AspNetCore.Components.Factory.IComponentActivator") 
                ?? throw new InvalidOperationException("The 'AI4E.AspNetCore.Components.Factory.IComponentActivator' type cannot be loaded");
        }
        private static Type GetDefaultComponentActivatorType()
        {
            return _componentsFactoryAssembly.GetType("AI4E.AspNetCore.Components.Factory.ComponentActivator") 
                ?? throw new InvalidOperationException("The 'AI4E.AspNetCore.Components.Factory.ComponentActivator' type cannot be loaded");
        }

        private static Func<object, Type, object> BuildComponentActivator()
        {
            var activateComponentMethod = _componentActivatorType.GetMethod("ActivateComponent");

            var componentActivator = Expression.Parameter(typeof(object), "componentActivator");
            var componentTypeParameter = Expression.Parameter(typeof(Type), "componentType");
            var convertedComponentActivator = Expression.Convert(componentActivator, _componentActivatorType);
            var activateComponentCall = Expression.Call(convertedComponentActivator, activateComponentMethod, componentTypeParameter);
            var convertedResult = Expression.Convert(activateComponentCall, typeof(object));
            var lambda = Expression.Lambda<Func<object, Type, object>>(convertedResult, componentActivator, componentTypeParameter);
            return lambda.Compile();
        }

        public static object InstantiateComponent(IServiceProvider serviceProvider, Type componentType)
        {
            var componentActivator = serviceProvider.GetService(_componentActivatorType);

            if (componentActivator is null)
            {
                componentActivator = _defaultComponentActivators.GetValue(serviceProvider, _buildDefaultComponentActivator);
            }

            return _componentActivator.Invoke(componentActivator, componentType);
        }

        private static object BuildDefaultComponentActivator(IServiceProvider serviceProvider)
        {
            return ActivatorUtilities.CreateInstance(serviceProvider, _defaultComponentActivatorType);
        }
    }
}
