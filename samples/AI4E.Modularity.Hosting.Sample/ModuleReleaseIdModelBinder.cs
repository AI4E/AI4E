using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.Modularity.Hosting.Sample
{
    public sealed class ModuleReleaseIdModelBinder : IModelBinder
    {
        private readonly ILogger<ModuleReleaseIdModelBinder> _logger;

        public ModuleReleaseIdModelBinder(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
                throw new ArgumentNullException(nameof(loggerFactory));

            _logger = loggerFactory.CreateLogger<ModuleReleaseIdModelBinder>();
        }

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
                throw new ArgumentNullException(nameof(bindingContext));

            // _logger.AttemptingToBindModel(bindingContext);

            var modelName = bindingContext.ModelName;
            var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

            if (valueProviderResult == ValueProviderResult.None)
            {
                //_logger.FoundNoValueInRequest(bindingContext);

                // no entry
                //_logger.DoneAttemptingToBindModel(bindingContext);
                return Task.CompletedTask;
            }

            var modelState = bindingContext.ModelState;
            modelState.SetModelValue(modelName, valueProviderResult);

            var metadata = bindingContext.ModelMetadata;
            var type = metadata.UnderlyingOrModelType;

            try
            {
                var value = valueProviderResult.FirstValue;
                var culture = valueProviderResult.Culture;

                object model;
                if (string.IsNullOrWhiteSpace(value))
                {
                    model = null;
                }
                else if (type == typeof(ModuleReleaseIdentifier))
                {
                    var parts = value.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length != 2)
                    {
                        model = null;
                    }
                    else
                    {
                        var module = new ModuleIdentifier(parts[0]);
                        var version = ModuleVersion.Parse(parts[1]);
                        model = new ModuleReleaseIdentifier(module, version);
                    }

                }
                else
                {
                    // unreachable
                    throw new NotSupportedException();
                }

                if (model == null && !metadata.IsReferenceOrNullableType)
                {
                    modelState.TryAddModelError(
                        modelName,
                        metadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(
                            valueProviderResult.ToString()));
                }
                else
                {
                    bindingContext.Result = ModelBindingResult.Success(model);
                }
            }
            catch (Exception exception)
            {
                modelState.TryAddModelError(modelName, exception, metadata);
            }

            //_logger.DoneAttemptingToBindModel(bindingContext);
            return Task.CompletedTask;
        }
    }

    public sealed class ModuleReleaseIdModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();

            if (context.Metadata.ModelType == typeof(ModuleReleaseIdentifier))
            {
                return new ModuleReleaseIdModelBinder(loggerFactory);
            }

            return null;
        }
    }
}
