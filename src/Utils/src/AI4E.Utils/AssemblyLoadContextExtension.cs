using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace System.Runtime.Loader
{
    public static class AI4EAspNetCoreComponentsAssemblyLoadContextExtension
    {
#if !SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT

        private static readonly Lazy<Action<AssemblyLoadContext>?> _unloadAssemblyLoadContext =
            new Lazy<Action<AssemblyLoadContext>?>(BuildUnloadAssemblyLoadContext, LazyThreadSafetyMode.PublicationOnly);

        private static Action<AssemblyLoadContext>? BuildUnloadAssemblyLoadContext()
        {
            var unloadMethod = typeof(AssemblyLoadContext).GetMethod(
                "Unload",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                Type.DefaultBinder,
                Type.EmptyTypes,
                modifiers: null);

            if (unloadMethod is null)
            {
                return null;
            }

            var assemblyLoadContextParameter = Expression.Parameter(typeof(AssemblyLoadContext), "assemblyLoadContext");
            var unloadMethodCall = Expression.Call(assemblyLoadContextParameter, unloadMethod);
            var lambda = Expression.Lambda<Action<AssemblyLoadContext>>(unloadMethodCall, assemblyLoadContextParameter);
            return lambda.Compile();
        }

        public static void Unload(this AssemblyLoadContext assemblyLoadContext)
        {
            var unloadAssemblyLoadContext = _unloadAssemblyLoadContext.Value;

            if (unloadAssemblyLoadContext is null)
            {
                throw new NotSupportedException("The current platform does not support collectible assembly load context instances.");
            }

            unloadAssemblyLoadContext(assemblyLoadContext);
        }
#endif
    }
}
