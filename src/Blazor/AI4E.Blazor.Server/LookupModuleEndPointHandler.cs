using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Blazor.Modularity;
using AI4E.Modularity;
using AI4E.Routing;

namespace AI4E.Blazor.Server
{
    [MessageHandler]
    internal sealed class LookupModuleEndPointHandler : MessageHandler
    {
        private readonly IRunningModuleLookup _runningModules;

        public LookupModuleEndPointHandler(IRunningModuleLookup runningModules)
        {
            if (runningModules == null)
                throw new ArgumentNullException(nameof(runningModules));

            _runningModules = runningModules;
        }

        public async Task<EndPointAddress> HandleAsync(LookupModuleEndPoint message, CancellationToken cancellation)
        {
            var endPoints = await _runningModules.GetEndPointsAsync(message.Module, cancellation);

            return endPoints.FirstOrDefault();
        }
    }
}
