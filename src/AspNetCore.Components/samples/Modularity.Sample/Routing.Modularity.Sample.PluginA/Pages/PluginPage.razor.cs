using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Extensibility;
using AI4E.Messaging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Routing.Modularity.Sample.PluginA.Pages
{
    public partial class PluginPage
    {
        private readonly Lazy<IMessageDispatcher> _messageDispatcher;

#nullable disable
        [Inject] private AssemblyManager AssemblyManager { get; set; }
#nullable enable

        private IMessageDispatcher MessageDispatcher => _messageDispatcher.Value;

        public PluginPage()
        {
            _messageDispatcher = new Lazy<IMessageDispatcher>(GetMessageDispatcher, LazyThreadSafetyMode.None);
        }

        private IMessageDispatcher GetMessageDispatcher()
        {
            var type = GetType();
            var assembly = type.Assembly;
            var serviceProvider = AssemblyManager.GetAssemblyServiceProvider(assembly);
            return serviceProvider?.GetRequiredService<IMessageDispatcher>() ?? NoMessageDispatcher.Instance;
        }

        private string? Str { get; set; }
        private IDispatchResult? DispatchResult { get; set; }
        private bool InProgress { get; set; }

        private async Task TransmitAsync()
        {
            if (InProgress)
                return;

            InProgress = true;
            DispatchResult = await MessageDispatcher.DispatchAsync(new HostMessage(Str));
            InProgress = false;
        }
    }
}
