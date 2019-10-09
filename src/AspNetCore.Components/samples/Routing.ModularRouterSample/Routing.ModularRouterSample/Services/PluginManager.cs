using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Extensibility;

namespace Routing.ModularRouterSample.Services
{
    public class PluginManager
    {
        private readonly AssemblyManager _assemblyManager;
        private readonly Dictionary<AssemblyName, Assembly> _hostAssemblies;
        private PluginAssemblyLoadContext _loadContext;
        private Assembly _pluginAssembly;

        public PluginManager(AssemblyManager assemblyManager)
        {
            _assemblyManager = assemblyManager;
            _hostAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToDictionary(p => p.GetName(), p => p, new AssemblyNameComparer());
        }

        private const string _pluginName = "Routing.ModularRouterSample.Plugin";

        private static string GetPluginPath()
        {
            var dir = Assembly.GetExecutingAssembly().Location;
            var targetFramework = Path.GetFileName(dir = GetDirectoryName(dir));
            var configuration = Path.GetFileName(dir = GetDirectoryName(dir));
            dir = GetDirectoryName(dir);
            dir = GetDirectoryName(dir);

            var pluginAssemblyDir = Path.Combine(dir, _pluginName, configuration, "netstandard2.0");
            return Path.Combine(pluginAssemblyDir, _pluginName + ".dll");
        }

        private static string GetDirectoryName(string path)
        {
            path = Path.GetDirectoryName(path);
            if (path.Last() == '/' || path.Last() == '\\')
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }

        public async Task InstallPluginAsync()
        {
            if (IsPluginInstalled)
                return;

            var assemblyPath = GetPluginPath();
            _loadContext = new PluginAssemblyLoadContext(assemblyPath, _hostAssemblies);
            _pluginAssembly = _loadContext.LoadFromAssemblyPath(assemblyPath);

            await _assemblyManager.AddAssemblyAsync(_pluginAssembly, _loadContext);
            IsPluginInstalled = true;
        }

        public async Task UninstallPluginAsync()
        {
            if (!IsPluginInstalled)
                return;

            await RemoveFromAssemblyManagerAsync();
            await Task.Yield(); // We are running on a sync-context. Allow the renderer to re-render.
            Unload(out var weakRef);

            for (var i = 0; weakRef.IsAlive && (i < 100); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (weakRef.IsAlive)
            {
                Debugger.Break();
                //throw new Exception("Unable to unload plugin.");
            }

            IsPluginInstalled = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async ValueTask RemoveFromAssemblyManagerAsync()
        {
            await _assemblyManager.RemoveAssemblyAsync(_pluginAssembly);
            RemoveFromInternalCaches(_pluginAssembly.Yield().ToHashSet());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Unload(out WeakReference weakRef)
        {
            _loadContext.Unload();
            weakRef = new WeakReference(_loadContext);
            _loadContext = null;
            _pluginAssembly = null;
        }

        private static readonly Action<HashSet<Assembly>> RemoveFromAttributeAuthorizeDataCache = RemoveFromCache(
            "Microsoft.AspNetCore.Components.Authorization",
            "Microsoft.AspNetCore.Components.Authorization.AttributeAuthorizeDataCache",
            "_cache");

        private static readonly Action<HashSet<Assembly>> RemoveFromFormatterDelegateCache = RemoveFromCache(
            "Microsoft.AspNetCore.Components",
            "Microsoft.AspNetCore.Components.BindConverter+FormatterDelegateCache",
            "_cache");

        private static readonly Action<HashSet<Assembly>> RemoveFromParserDelegateCache = RemoveFromCache(
           "Microsoft.AspNetCore.Components",
           "Microsoft.AspNetCore.Components.BindConverter+ParserDelegateCache",
           "_cache");

        private static readonly Action<HashSet<Assembly>> RemoveFromCascadingParameterState = RemoveFromCache(
           "Microsoft.AspNetCore.Components",
           "Microsoft.AspNetCore.Components.CascadingParameterState",
           "_cachedInfos");

        private static readonly Action<HashSet<Assembly>> RemoveFromComponentFactory
            = BuildRemoveFromComponentFactory();

        private static readonly Action<HashSet<Assembly>> RemoveFromComponentProperties = RemoveFromCache(
         "Microsoft.AspNetCore.Components",
         "Microsoft.AspNetCore.Components.Reflection.ComponentProperties",
         "_cachedWritersByType");

        private static readonly Action<HashSet<Assembly>> RemoveFromInternalCaches =
            RemoveFromAttributeAuthorizeDataCache +
            RemoveFromFormatterDelegateCache +
            RemoveFromParserDelegateCache +
            RemoveFromCascadingParameterState +
            RemoveFromComponentFactory +
            RemoveFromComponentProperties;

        private static Action<HashSet<Assembly>> RemoveFromCache(
            string assembly,
            string typeName,
            string fieldName,
            object instance = null)
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

            void RemoveFromCache(HashSet<Assembly> unloaded)
            {
                var typesToRemove = cache.Keys.OfType<Type>().Where(p => unloaded.Contains(p.Assembly)).ToList();

                foreach (var type in typesToRemove)
                {
                    cache.Remove(type);
                }
            }

            return RemoveFromCache;
        }

        private static Action<HashSet<Assembly>> BuildRemoveFromComponentFactory()
        {
            var assembly = "Microsoft.AspNetCore.Components";
            var typeName = "Microsoft.AspNetCore.Components.ComponentFactory";
            var fieldName = "_cachedInitializers";

            var type = GetType(assembly, typeName);
            var instance = type.GetField(
                "Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
            return RemoveFromCache(assembly, typeName, fieldName, instance);
        }

        private static Type GetType(string assembly, string typeName)
        {
            return Type.GetType($"{typeName}, {assembly}")
            ?? throw new Exception($"Unable to reflect type '{typeName}, {assembly}'.");
        }

        public bool IsPluginInstalled { get; private set; }

        // https://github.com/dotnet/samples/blob/master/core/tutorials/Unloading/Host/Program.cs
        private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;
            private readonly Dictionary<AssemblyName, Assembly> _hostAssemblies;

            public PluginAssemblyLoadContext(string pluginPath, Dictionary<AssemblyName, Assembly> hostAssemblies) : base(isCollectible: true)
            {
                _resolver = new AssemblyDependencyResolver(pluginPath);
                _hostAssemblies = hostAssemblies;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (_hostAssemblies.TryGetValue(assemblyName, out var assembly))
                {
                    return assembly;
                }

                var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
                if (assemblyPath != null)
                {
                    Console.WriteLine($"Loading assembly {assemblyPath} into the HostAssemblyLoadContext");
                    return LoadFromAssemblyPath(assemblyPath);
                }

                return null;
            }
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public bool Equals(AssemblyName x, AssemblyName y)
            {
                return string.Equals(x?.FullName, y?.FullName, StringComparison.Ordinal);
            }

            public int GetHashCode(AssemblyName obj)
            {
                return obj.FullName.GetHashCode();
            }
        }
    }
}
