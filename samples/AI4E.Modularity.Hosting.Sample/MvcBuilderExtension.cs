using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Modularity.Hosting.Sample
{
    public static class MvcBuilderExtension
    {
        public static IMvcBuilder AddModuleModelBinders(this IMvcBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var services = builder.Services;

            services.Configure<MvcOptions>(options =>
            {
                options.ModelBinderProviders.Insert(0, new ModuleIdModelBinderProvider());
                options.ModelBinderProviders.Insert(0, new ModuleReleaseIdModelBinderProvider());
            });

            return builder;
        }
    }
}
