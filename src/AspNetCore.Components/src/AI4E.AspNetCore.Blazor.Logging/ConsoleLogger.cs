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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * .NET Extensions (https://github.com/aspnet/Extensions)
 * Copyright (c) .NET Foundation. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use
 * these files except in compliance with the License. You may obtain a copy of the
 * License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed
 * under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 * CONDITIONS OF ANY KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations under the License.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace AI4E.AspNetCore.Blazor.Logging
{
    internal sealed class ConsoleLogger : ILogger
    {
        private const string LoglevelPadding = ": ";
        private static readonly string MessagePadding;
        private static readonly string NewLineWithMessagePadding;

        private static readonly int[] ConsoleColor2RGB =
        {
             0x000000,
             0x000080,
             0x008000,
             0x008080,
             0x800000,
             0x800080,
             0x808000,
             0xC0C0C0,
             0x808080,
             0x0000FF,
             0x00FF00,
             0x00FFFF,
             0xFF0000,
             0xFF00FF,
             0xFFFF00,
             0xFFFFFF
        };

        // ConsoleColor does not have a value to specify the 'Default' color
        private readonly ConsoleColor? _defaultConsoleColor = null;

        [ThreadStatic]
        private static StringBuilder? _logBuilder;

        private readonly string _name;
        private readonly IJSInProcessRuntime _jsRuntime;

        internal IExternalScopeProvider? ScopeProvider { get; set; }

        internal ConsoleLoggerOptions? Options { get; set; }

#pragma warning disable CA1810
        static ConsoleLogger()
#pragma warning restore CA1810
        {
            var logLevelString = GetLogLevelString(LogLevel.Information);
            MessagePadding = new string(' ', logLevelString.Length + LoglevelPadding.Length);
            NewLineWithMessagePadding = Environment.NewLine + MessagePadding;
        }

        public ConsoleLogger(string name, IJSInProcessRuntime jsRuntime)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (jsRuntime == null)
                throw new ArgumentNullException(nameof(jsRuntime));

            _name = name;
            _jsRuntime = jsRuntime;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);

            if (!string.IsNullOrEmpty(message) || exception != null)
            {
                WriteMessage(logLevel, _name, eventId.Id, message, exception);
            }
        }

        public void WriteMessage(LogLevel logLevel, string logName, int eventId, string message, Exception exception)
        {
            var logBuilder = _logBuilder;
            _logBuilder = null;

            if (logBuilder == null)
            {
                logBuilder = new StringBuilder();
            }

            // Example:
            // INFO: ConsoleApp.Program[10]
            //       Request received

            var logLevelColors = GetLogLevelConsoleColors(logLevel);
            var logLevelString = GetLogLevelString(logLevel);
            logBuilder.Append(logLevelString);
            // category and event id
            logBuilder.Append(LoglevelPadding);
            logBuilder.Append(logName);
            logBuilder.Append("[");
            logBuilder.Append(eventId);
            logBuilder.AppendLine("]");

            // scope information
            GetScopeInformation(logBuilder);

            if (!string.IsNullOrEmpty(message))
            {
                // message
                logBuilder.Append(MessagePadding);

                var len = logBuilder.Length;
                logBuilder.AppendLine(message);
                logBuilder.Replace(Environment.NewLine, NewLineWithMessagePadding, len, message.Length);
            }

            // Example:
            // System.InvalidOperationException
            //    at Namespace.Class.Function() in File:line X
            if (exception != null)
            {
                // exception message
                logBuilder.AppendLine(exception.ToString());
            }

            _jsRuntime.Invoke<object>(
                "ai4e.console.log",
                logBuilder.ToString(),
                ToRGB(logLevelColors.Foreground),
                ToRGB(logLevelColors.Background));

            //Console.Write(logBuilder.ToString());

            logBuilder.Clear();
            if (logBuilder.Capacity > 1024)
            {
                logBuilder.Capacity = 1024;
            }
            _logBuilder = logBuilder;
        }

        private static string? ToRGB(ConsoleColor? logLevelColor)
        {
            if (logLevelColor == null)
                return null;

            return "#" + ConsoleColor2RGB[(int)logLevelColor].ToString("X6", CultureInfo.InvariantCulture);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return ScopeProvider?.Push(state) ?? NullScope.Instance;
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel)),
            };
        }

        private ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
        {
            if (Options?.DisableColors ?? false)
            {
                return new ConsoleColors(null, null);
            }

            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            return logLevel switch
            {
                LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.Red),
                LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.Red),
                LogLevel.Warning => new ConsoleColors(_defaultConsoleColor, ConsoleColor.Yellow),
                LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, _defaultConsoleColor),
                _ => new ConsoleColors(_defaultConsoleColor, _defaultConsoleColor),
            };
        }

        private void GetScopeInformation(StringBuilder stringBuilder)
        {
            var scopeProvider = ScopeProvider;
            if ((Options?.IncludeScopes ?? false) && scopeProvider != null)
            {
                var initialLength = stringBuilder.Length;

                scopeProvider.ForEachScope((scope, state) =>
                {
                    var (builder, length) = state;
                    var first = length == builder.Length;
                    builder.Append(first ? "=> " : " => ").Append(scope);
                }, (stringBuilder, initialLength));

                if (stringBuilder.Length > initialLength)
                {
                    stringBuilder.Insert(initialLength, MessagePadding);
                    stringBuilder.AppendLine();
                }
            }
        }

        private readonly struct ConsoleColors
        {
            public ConsoleColors(ConsoleColor? foreground, ConsoleColor? background)
            {
                Foreground = foreground;
                Background = background;
            }

            public ConsoleColor? Foreground { get; }

            public ConsoleColor? Background { get; }
        }
    }

    /// <summary>
    /// An empty scope without any logic
    /// </summary>
    internal class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new NullScope();

        private NullScope()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Represents options that control the <see cref="ConsoleLogger"/> behavior.
    /// </summary>
    public class ConsoleLoggerOptions
    {
        /// <summary>
        /// Gets or sets a boolean value indicating whether the logger shall include scoped in the output.
        /// </summary>
        public bool IncludeScopes { get; set; }

        /// <summary>
        /// Gets or sets a boolean value indicating whether colors are disabled in the output.
        /// </summary>
        public bool DisableColors { get; set; }
    }
}
