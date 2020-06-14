using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AI4E.Messaging;

namespace AI4E.AspNetCore.Components
{
    public abstract class ListComponentBase<TModel> : ComponentBase<List<TModel>>
    {
        protected override bool TryExtractModelAsync(
            IDispatchResult dispatchResult, 
            [NotNullWhen(true)] out List<TModel>? model)
        {
            var success = dispatchResult.IsSuccessWithResults<TModel>(out var resources);
            model = success ? (resources as List<TModel>) ?? resources.ToList() : new List<TModel>();
            return success;
        }
    }
}
