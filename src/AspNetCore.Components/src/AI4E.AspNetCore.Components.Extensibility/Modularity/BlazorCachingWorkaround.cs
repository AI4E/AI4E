using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal static class BlazorCachingWorkaround
    {
        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromAttributeAuthorizeDataCache = RemoveFromCache(
        "Microsoft.AspNetCore.Components.Authorization",
        "Microsoft.AspNetCore.Components.Authorization.AttributeAuthorizeDataCache",
        "_cache");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromFormatterDelegateCache = RemoveFromCache(
            "Microsoft.AspNetCore.Components",
            "Microsoft.AspNetCore.Components.BindConverter+FormatterDelegateCache",
            "_cache");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromParserDelegateCache = RemoveFromCache(
           "Microsoft.AspNetCore.Components",
           "Microsoft.AspNetCore.Components.BindConverter+ParserDelegateCache",
           "_cache");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromCascadingParameterState = RemoveFromCache(
           "Microsoft.AspNetCore.Components",
           "Microsoft.AspNetCore.Components.CascadingParameterState",
           "_cachedInfos");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromComponentFactory
            = BuildRemoveFromComponentFactory();

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromComponentProperties = RemoveFromCache(
         "Microsoft.AspNetCore.Components",
         "Microsoft.AspNetCore.Components.Reflection.ComponentProperties",
         "_cachedWritersByType");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromInternalCaches =
            RemoveFromAttributeAuthorizeDataCache +
            RemoveFromFormatterDelegateCache +
            RemoveFromParserDelegateCache +
            RemoveFromCascadingParameterState +
            RemoveFromComponentFactory +
            RemoveFromComponentProperties;

        private static Action<ImmutableHashSet<Assembly>> RemoveFromCache(
            string assembly,
            string typeName,
            string fieldName,
            object? instance = null)
        {
            var type = GetType(assembly, typeName);

            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic;

            if (instance is null)
            {
                bindingFlags |= BindingFlags.Static;
            }
            else
            {
                bindingFlags |= BindingFlags.Instance;
            }

            var field = type.GetField(fieldName, bindingFlags)
                ?? throw new Exception($"Unable to reflect field '{fieldName}' of type '{type}'");

            if (!(field.GetValue(instance) is IDictionary cache))
                return _ => { };

            void RemoveFromCache(ImmutableHashSet<Assembly> unloaded)
            {
                var typesToRemove = cache.Keys
                    .OfType<Type>()
                    .Where(p => unloaded.Contains(p.Assembly))
                    .ToList();

                foreach (var type in typesToRemove)
                {
                    cache.Remove(type);
                }
            }

            return RemoveFromCache;
        }

        private static Action<ImmutableHashSet<Assembly>> BuildRemoveFromComponentFactory()
        {
            var assembly = "Microsoft.AspNetCore.Components";
            var typeName = "Microsoft.AspNetCore.Components.ComponentFactory";
            var fieldName = "_cachedInitializers";

            var type = GetType(assembly, typeName);
            var instance = type.GetField(
                "Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                !.GetValue(null);
            return RemoveFromCache(assembly, typeName, fieldName, instance);
        }

        private static Type GetType(string assembly, string typeName)
        {
            return Type.GetType($"{typeName}, {assembly}")
            ?? throw new Exception($"Unable to reflect type '{typeName}, {assembly}'.");
        }

        public static void Configure(IBlazorModularityBuilder builder)
        {
            builder.ConfigureCleanup(RemoveFromInternalCaches);
        }
    }
}
