using System;
using System.IO;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Modularity
{
    public abstract class ViewExtensionRenderer : MessageHandler
    {
        protected ViewExtensionRenderer()
        {

        }

        [NoAction]
        public virtual SuccessDispatchResult<string> View(string view)
        {
            return View<object>(view, null);
        }

        [NoAction]
        public virtual SuccessDispatchResult<string> View<TModel>(string view, TModel model)
        {
            var serviceProvider = Context.DispatchServices;
            var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
            var tempDataProvider = serviceProvider.GetRequiredService<ITempDataProvider>();

            return new SuccessDispatchResult<string>(RenderViewToString(view, model).GetAwaiter().GetResult()); // TODO
        }

        private async Task<string> RenderViewToString<TModel>(string name, TModel model)
        {
            var serviceProvider = Context.DispatchServices;
            var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
            var tempDataProvider = serviceProvider.GetRequiredService<ITempDataProvider>();
            var actionContext = GetActionContext();

            var viewEngineResult = viewEngine.FindView(actionContext, name, false);

            if (!viewEngineResult.Success)
            {
                throw new InvalidOperationException(string.Format("Couldn't find view '{0}'", name));
            }

            var view = viewEngineResult.View;

            using (var output = new StringWriter())
            {
                var viewContext = new ViewContext(
                    actionContext,
                    view,
                    new ViewDataDictionary<TModel>(
                        metadataProvider: new EmptyModelMetadataProvider(),
                        modelState: new ModelStateDictionary())
                    {
                        Model = model
                    },
                    new TempDataDictionary(
                        actionContext.HttpContext,
                        tempDataProvider),
                    output,
                    new HtmlHelperOptions());

                await view.RenderAsync(viewContext);

                return output.ToString();
            }
        }

        private ActionContext GetActionContext()
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = Context.DispatchServices
            };

            return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        }
    }
}
