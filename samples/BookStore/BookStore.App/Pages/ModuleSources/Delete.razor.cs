using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using BookStore.Commands;
using BookStore.Models;
using Microsoft.AspNetCore.Components;

namespace BookStore.App.Pages.ModuleSources
{
    public abstract class Delete_ : ComponentBase<ModuleSourceDeleteModel>
    {
        [Inject] private IMessageDispatcher MessageDispatcher { get; set; }
        [Inject] private IUriHelper UriHelper { get; set; }

        [Parameter] private Guid Id { get; set; }

        protected override async ValueTask<ModuleSourceDeleteModel>
            LoadModelAsync(CancellationToken cancellation)
        {
            var queryResult = await MessageDispatcher.QueryByIdAsync<ModuleSourceDeleteModel>
                (Id, cancellation);
            return await EvaluateLoadResultAsync(queryResult);
        }

        public async Task DeleteAsync()
        {
            var command = new ModuleSourceDeleteCommand(Id, Model.ConcurrencyToken);
            var commandResult = await MessageDispatcher.DispatchAsync(command); // TODO: Cancellation

            await EvaluateStoreResultAsync(commandResult);

            if (commandResult.IsSuccess)
            {
                UriHelper.NavigateTo("/modulesources");
            }
        }
    }
}
