using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E;
using AI4E.Async;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Utils
{
    public static class HtmlHelperExtension // TODO: Move to AI4E
    {
        public static async Task<string> RenderViewExtensionAsync<TViewExtension>(this IMessageDispatcher messageDispatcher)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            var dispatchResult = await messageDispatcher.DispatchAsync<TViewExtension>();

            if (!dispatchResult.IsAggregateResult() && dispatchResult.IsSuccess)
            {
                var result = (dispatchResult as IDispatchResult<string>)?.Result;

                if (result != null)
                {
                    return result;
                }
            }
            else if (dispatchResult.IsAggregateResult(out var aggregateResult))
            {
                aggregateResult = aggregateResult.Flatten();

                var contentBuilder = new StringBuilder();

                foreach (var r in aggregateResult.DispatchResults)
                {
                    var result = (r as IDispatchResult<string>)?.Result;

                    if (result != null)
                    {
                        contentBuilder.Append(result);
                    }
                }

                if (contentBuilder.Length == 0)
                {
                    return string.Empty;
                }

                return contentBuilder.ToString();
            }

            return string.Empty;
        }

        public static async Task<string> RenderViewExtensionAsync<TViewExtension>(this IMessageDispatcher messageDispatcher, TViewExtension viewExtension)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            var dispatchResult = await messageDispatcher.DispatchAsync(viewExtension);

            if (!dispatchResult.IsAggregateResult() && dispatchResult.IsSuccess)
            {
                var result = (dispatchResult as IDispatchResult<string>)?.Result;

                if (result != null)
                {
                    return result;
                }
            }
            else if (dispatchResult.IsAggregateResult(out var aggregateResult))
            {
                aggregateResult = aggregateResult.Flatten();

                var contentBuilder = new StringBuilder();

                foreach (var r in aggregateResult.DispatchResults)
                {
                    var result = (r as IDispatchResult<string>)?.Result;

                    if (result != null)
                    {
                        contentBuilder.Append(result);
                    }
                }

                if (contentBuilder.Length == 0)
                {
                    return string.Empty;
                }

                return contentBuilder.ToString();
            }

            return string.Empty;
        }

        public static Task<IHtmlContent> RenderViewExtensionAsync<TViewExtension>(this IHtmlHelper html)
        {
            if (html == null)
                throw new ArgumentNullException(nameof(html));

            var viewExtension = default(TViewExtension);

            try
            {
                viewExtension = Activator.CreateInstance<TViewExtension>();
            }
            catch (MissingMethodException exc)
            {
                throw new ArgumentException("The specified view extension must have a parameterless constructor.", exc);
            }

            Debug.Assert(viewExtension != null);

            return html.RenderViewExtensionAsync(viewExtension);
        }

        public static async Task<IHtmlContent> RenderViewExtensionAsync<TViewExtension>(this IHtmlHelper html, TViewExtension viewExtension)
        {
            if (html == null)
                throw new ArgumentNullException(nameof(html));

            var services = html.ViewContext?.HttpContext?.RequestServices;

            if (services == null)
            {
                throw new InvalidOperationException("Unable to gte request services.");
            }

            var dispatcher = services.GetRequiredService<IMessageDispatcher>();
            var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(3000));
            var cancellation = cancellationSource.Token;
            var dispatchResult = default(IDispatchResult);

            try
            {
                dispatchResult = await dispatcher.DispatchAsync(viewExtension, new DispatchValueDictionary(), publish: true, cancellation);//.WithCancellation(cancellation);
            }
            catch (OperationCanceledException)
            {
                // TODO: Log
                return HtmlString.Empty;
            }

            if (!dispatchResult.IsAggregateResult() && dispatchResult.IsSuccess)
            {
                var result = (dispatchResult as IDispatchResult<string>)?.Result;

                if (result != null)
                {
                    return new HtmlString(result);
                }
            }
            else if (dispatchResult.IsAggregateResult(out var aggregateResult))
            {
                aggregateResult = aggregateResult.Flatten();

                var contentBuilder = new StringBuilder();

                foreach (var r in aggregateResult.DispatchResults)
                {
                    var result = (r as IDispatchResult<string>)?.Result;

                    if (result != null)
                    {
                        contentBuilder.Append(result);
                    }
                }

                if (contentBuilder.Length == 0)
                {
                    return HtmlString.Empty;
                }

                return new HtmlString(contentBuilder.ToString());
            }

            return HtmlString.Empty;
        }
    }
}
