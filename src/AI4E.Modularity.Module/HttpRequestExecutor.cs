using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace AI4E.Modularity.Module
{
    internal sealed class HttpRequestExecutor<TContext> : IHttpRequestExecutor
    {
        private readonly IHttpApplication<TContext> _application;
        private readonly ILogger<HttpRequestExecutor<TContext>> _logger;

        public HttpRequestExecutor(IHttpApplication<TContext> application, ILogger<HttpRequestExecutor<TContext>> logger = null)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            _application = application;
            _logger = logger;
        }

        public async ValueTask<ModuleHttpResponse> ExecuteAsync(ModuleHttpRequest request, CancellationToken cancellation)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var requestMessage = request;
            var responseStream = new MemoryStream();
            var requestFeature = BuildRequestFeature(requestMessage);
            var responseFeature = BuildResponseFeature(responseStream);
            var features = BuildFeatureCollection(requestFeature, responseFeature);
            var context = _application.CreateContext(features);

            try
            {
                await _application.ProcessRequestAsync(context);
                var responseMessage = BuildResponseMessage(responseStream, responseFeature);

                ExceptionHelper.HandleExceptions(() => _application.DisposeContext(context, exception: null), _logger);

                return responseMessage;
            }
            catch (Exception exc)
            {
                ExceptionHelper.HandleExceptions(() => _application.DisposeContext(context, exc), _logger);

                throw;
            }
        }

        private static ModuleHttpResponse BuildResponseMessage(MemoryStream responseStream, HttpResponseFeature responseFeature)
        {
            var response = new ModuleHttpResponse
            {
                StatusCode = responseFeature.StatusCode,
                ReasonPhrase = responseFeature.ReasonPhrase,
                Body = responseStream.ToArray(),
                Headers = new Dictionary<string, string[]>()
            };

            foreach (var entry in responseFeature.Headers)
            {
                response.Headers.Add(entry.Key, entry.Value.ToArray());
            }

            return response;
        }

        private static FeatureCollection BuildFeatureCollection(HttpRequestFeature requestFeature, HttpResponseFeature responseFeature)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(requestFeature);
            features.Set<IHttpResponseFeature>(responseFeature);
            return features;
        }

        private HttpResponseFeature BuildResponseFeature(MemoryStream responseStream)
        {
            return new HttpResponseFeature() { Body = responseStream, Headers = new HeaderDictionary() };
        }

        private static HttpRequestFeature BuildRequestFeature(ModuleHttpRequest message)
        {
            var requestFeature = new HttpRequestFeature
            {
                Method = message.Method,
                Path = message.Path,
                PathBase = message.PathBase,
                Protocol = message.Protocol,
                QueryString = message.QueryString,
                RawTarget = message.RawTarget,
                Scheme = message.Scheme,
                Body = new MemoryStream(message.Body),
                Headers = new HeaderDictionary()
            };

            foreach (var header in message.Headers)
            {
                requestFeature.Headers.Add(header.Key, new StringValues(header.Value));
            }

            return requestFeature;
        }
    }
}
