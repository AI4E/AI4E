using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using BookStore.Models;
using Microsoft.AspNetCore.Components;

namespace BookStore.App.Pages.ModuleSources
{
    public abstract class List_ : ComponentBase<IEnumerable<ModuleSourceListModel>>
    {
        [Inject] private IMessageDispatcher MessageDispatcher { get; set; }

        protected override async ValueTask<IEnumerable<ModuleSourceListModel>> LoadModelAsync(CancellationToken cancellation)
        {
            var dispatchResult = await MessageDispatcher.QueryAsync<IEnumerable<ModuleSourceListModel>>(cancellation);
            return await EvaluateLoadResultAsync(dispatchResult);
        }
    }
}
