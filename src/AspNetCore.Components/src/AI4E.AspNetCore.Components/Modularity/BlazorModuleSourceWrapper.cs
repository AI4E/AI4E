using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleSourceWrapper : IBlazorModuleSource
    {
        private readonly IBlazorModuleSource _moduleSource;
        private readonly Func<IBlazorModuleDescriptor, IBlazorModuleDescriptor> _processor;

        private readonly ConditionalWeakTable<IBlazorModuleDescriptor, IBlazorModuleDescriptor> _cache;
        private readonly ConditionalWeakTable<IBlazorModuleDescriptor, IBlazorModuleDescriptor>.CreateValueCallback _processUncached;

        public BlazorModuleSourceWrapper(
            IBlazorModuleSource moduleSource,
            Func<IBlazorModuleDescriptor, IBlazorModuleDescriptor> processor)
        {
            if (moduleSource is null)
                throw new ArgumentNullException(nameof(moduleSource));

            if (processor is null)
                throw new ArgumentNullException(nameof(processor));

            _moduleSource = moduleSource;
            _processor = processor;

            _cache = new ConditionalWeakTable<IBlazorModuleDescriptor, IBlazorModuleDescriptor>();
            _processUncached = ProcessUncached; // Cache delegate for perf reasons.
        }

        public IAsyncEnumerable<IBlazorModuleDescriptor> GetModulesAsync(CancellationToken cancellation)
        {
            return _moduleSource.GetModulesAsync(cancellation).Select(Process);
        }

        private IBlazorModuleDescriptor Process(IBlazorModuleDescriptor moduleSource)
        {
            return _cache.GetValue(moduleSource, _processUncached);
        }

        private IBlazorModuleDescriptor ProcessUncached(IBlazorModuleDescriptor moduleSource)
        {
            return _processor(moduleSource);
        }

        public event EventHandler? ModulesChanged
        {
            add
            {
                _moduleSource.ModulesChanged += value;
            }
            remove
            {
                _moduleSource.ModulesChanged -= value;
            }
        }
    }

    public static class BlazorModuleSourceExtension
    {
        public static IBlazorModuleSource Configure(
            this IBlazorModuleSource moduleSource,
            Func<IBlazorModuleDescriptor, IBlazorModuleDescriptor> processor)
        {
            if (processor is null)
                throw new ArgumentNullException(nameof(processor));

            return new BlazorModuleSourceWrapper(moduleSource, processor);
        }
    }
}
