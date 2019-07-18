using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using BookStore.Commands;
using BookStore.Models;
using Microsoft.AspNetCore.Components;

namespace BookStore.App.Pages.ModuleSources
{
    public abstract class Create_ : ComponentBase<ModuleSourceCreateModel>
    {
        [Inject] private IMessageDispatcher MessageDispatcher { get; set; }
        [Inject] private IUriHelper UriHelper { get; set; }

        protected override ValueTask<ModuleSourceCreateModel> LoadModelAsync(CancellationToken cancellation)
        {
            return new ValueTask<ModuleSourceCreateModel>(new ModuleSourceCreateModel());
        }

        public async Task StoreAsync()
        {
            var command = new ModuleSourceCreateCommand(Guid.NewGuid(), Model.Name, Model.Location);
            var commandResult = await MessageDispatcher.DispatchAsync(command); // TODO: Cancellation

            await EvaluateStoreResultAsync(commandResult);

            if (commandResult.IsSuccess)
            {
                UriHelper.NavigateTo("/modulesources/" + command.Id.ToString());
            }
        }
    }
}
