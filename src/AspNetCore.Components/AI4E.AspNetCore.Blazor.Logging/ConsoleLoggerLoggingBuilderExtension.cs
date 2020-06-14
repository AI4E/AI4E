/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using AI4E.AspNetCore.Blazor.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Contains extensions for the <see cref="ILoggingBuilder"/> type.
    /// </summary>
    public static class ConsoleLoggerLoggingBuilderExtension
    {
        /// <summary>
        /// Adds browser console logging to the <see cref="ILoggingBuilder"/>.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <returns>The logging builder with the browser console logging added.</returns>
        public static ILoggingBuilder AddBrowserConsole(this ILoggingBuilder builder)
        {
#pragma warning disable CA1062
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ConsoleLoggerProvider>());
#pragma warning restore CA1062
            return builder;
        }

        /// <summary>
        /// Adds browser console logging to the <see cref="ILoggingBuilder"/>.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="configure">A configuration for the <see cref="ConsoleLoggerOptions"/>.</param>
        /// <returns>The logging builder with the browser console logging added.</returns>
        public static ILoggingBuilder AddBrowserConsole(
            this ILoggingBuilder builder, Action<ConsoleLoggerOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddBrowserConsole();
#pragma warning disable CA1062
            builder.Services.Configure(configure);
#pragma warning restore CA1062

            return builder;
        }
    }
}
