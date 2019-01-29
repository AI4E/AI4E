using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore
{
    public static class HtmlHelperExtension
    {
        public static async Task<string> RenderViewExtensionAsync<TViewExtension>(this IMessageDispatcher messageDispatcher)
            where TViewExtension : class, new()
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
                var flattenedDispatchResult = aggregateResult.Flatten();
                var contentBuilder = new StringBuilder();

                if (flattenedDispatchResult is IAggregateDispatchResult adr)
                {

                    foreach (var r in adr.DispatchResults)
                    {
                        var result = (r as IDispatchResult<string>)?.Result;

                        if (result != null)
                        {
                            contentBuilder.Append(result);
                        }
                    }
                }
                else
                {
                    var result = (flattenedDispatchResult as IDispatchResult<string>)?.Result;

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
             where TViewExtension : class
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
                var flattenedDispatchResult = aggregateResult.Flatten();
                var contentBuilder = new StringBuilder();

                if (flattenedDispatchResult is IAggregateDispatchResult adr)
                {

                    foreach (var r in adr.DispatchResults)
                    {
                        var result = (r as IDispatchResult<string>)?.Result;

                        if (result != null)
                        {
                            contentBuilder.Append(result);
                        }
                    }
                }
                else
                {
                    var result = (flattenedDispatchResult as IDispatchResult<string>)?.Result;

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
             where TViewExtension : class, new()
        {
            if (html == null)
                throw new ArgumentNullException(nameof(html));

            return html.RenderViewExtensionAsync(new TViewExtension());
        }

        public static async Task<IHtmlContent> RenderViewExtensionAsync<TViewExtension>(this IHtmlHelper html, TViewExtension viewExtension)
            where TViewExtension : class
        {
            if (html == null)
                throw new ArgumentNullException(nameof(html));

            var services = html.ViewContext?.HttpContext?.RequestServices;

            if (services == null)
            {
                throw new InvalidOperationException("Unable to get request services.");
            }

            var dispatcher = services.GetRequiredService<IMessageDispatcher>();
            var cancellationSource = new CancellationTokenSource(/*TimeSpan.FromMilliseconds(3000)*/);
            var cancellation = cancellationSource.Token;
            var dispatchResult = default(IDispatchResult);

            try
            {
                dispatchResult = await dispatcher.DispatchAsync(viewExtension, publish: true, cancellation).AsTask().WithCancellation(cancellation);
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
                var flattenedDispatchResult = aggregateResult.Flatten();
                var contentBuilder = new StringBuilder();

                if (flattenedDispatchResult is IAggregateDispatchResult adr)
                {

                    foreach (var r in adr.DispatchResults)
                    {
                        var result = (r as IDispatchResult<string>)?.Result;

                        if (result != null)
                        {
                            contentBuilder.Append(result);
                        }
                    }
                }
                else
                {
                    var result = (flattenedDispatchResult as IDispatchResult<string>)?.Result;

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
