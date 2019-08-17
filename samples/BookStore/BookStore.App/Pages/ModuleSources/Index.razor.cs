using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using BookStore.Commands;
using BookStore.Models;
using Microsoft.AspNetCore.Components;

namespace BookStore.App.Pages.ModuleSources
{
    public abstract class Index_ : ComponentBase<ModuleSourceModel>
    {
        [Inject] private IMessageDispatcher MessageDispatcher { get; set; }

        [Parameter] private Guid Id { get; set; }

        protected override async ValueTask<ModuleSourceModel>
            LoadModelAsync(CancellationToken cancellation)
        {
            var queryResult = await MessageDispatcher.QueryByIdAsync<ModuleSourceModel>
                (Id, cancellation);
            return await EvaluateLoadResultAsync(queryResult);
        }

        public async Task RenameAsync()
        {
            var command = new ModuleSourceRenameCommand(Id, Model.ConcurrencyToken, Model.Name);
            var commandResult = await MessageDispatcher.DispatchAsync(command); // TODO: Cancellation
            await EvaluateStoreResultAsync(commandResult);
        }
    }
}
