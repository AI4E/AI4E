using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal static class AutofacCachingWorkaround
    {
        private static void RemoveFromInternalCaches(ImmutableHashSet<Assembly> unloaded)
        {
            var autofac = Assembly.Load("Autofac");

            void RemoveFromConstructorParameterBinding(ImmutableHashSet<Assembly> unloaded)
            {
                var constructorParameterBindingType = autofac.GetType("Autofac.Core.Activators.Reflection.ConstructorParameterBinding");
                var constructorInvokers = (IDictionary)constructorParameterBindingType!.GetField("ConstructorInvokers", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var keysToRemove = constructorInvokers.Keys
                    .OfType<ConstructorInfo>()
                    .Where(p => unloaded.Contains(p.DeclaringType!.Assembly))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    constructorInvokers.Remove(key);
                }
            }

            void RemoveFromAutowiringPropertyInjector(ImmutableHashSet<Assembly> unloaded)
            {
                var autowiringPropertyInjectorType = autofac.GetType("Autofac.Core.Activators.Reflection.AutowiringPropertyInjector");
                var propertySetters = (IDictionary)autowiringPropertyInjectorType!.GetField("PropertySetters", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var propertySettersKeysToRemove = propertySetters.Keys
                    .OfType<PropertyInfo>()
                    .Where(p => unloaded.Contains(p.DeclaringType!.Assembly))
                    .ToList();

                foreach (var key in propertySettersKeysToRemove)
                {
                    propertySetters.Remove(key);
                }

                var injectableProperties = (IDictionary)autowiringPropertyInjectorType!.GetField("InjectableProperties", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var injectablePropertiesKeysToRemove = injectableProperties.Keys
                    .OfType<Type>()
                    .Where(p => unloaded.Contains(p.Assembly))
                    .ToList();

                foreach (var key in injectablePropertiesKeysToRemove)
                {
                    injectableProperties.Remove(key);
                }
            }

            void RemoveFromDefaultConstructorFinder(ImmutableHashSet<Assembly> unloaded)
            {
                var defaultConstructorFinderType = autofac.GetType("Autofac.Core.Activators.Reflection.DefaultConstructorFinder");
                var defaultPublicConstructorsCache = (IDictionary)defaultConstructorFinderType!.GetField("DefaultPublicConstructorsCache", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var defaultPublicConstructorsCacheKeysToRemove = defaultPublicConstructorsCache.Keys
                    .OfType<Type>()
                    .Where(p => unloaded.Contains(p.Assembly))
                    .ToList();

                foreach (var key in defaultPublicConstructorsCacheKeysToRemove)
                {
                    defaultPublicConstructorsCache.Remove(key);
                }
            }

            RemoveFromConstructorParameterBinding(unloaded);
            RemoveFromAutowiringPropertyInjector(unloaded);
            RemoveFromDefaultConstructorFinder(unloaded);
        }

        public static void Configure(IBlazorModularityBuilder builder)
        {
            builder.ConfigureCleanup(RemoveFromInternalCaches);
        }
    }
}
