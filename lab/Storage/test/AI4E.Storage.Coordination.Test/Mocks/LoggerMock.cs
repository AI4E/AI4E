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
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace AI4E.Storage.Coordination.Mocks
{
    public sealed class LoggerMock<TCategoryName> : ILogger<TCategoryName>
    {
        private readonly List<LogMessage> _recordedLogMessages = new List<LogMessage>();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _recordedLogMessages.Add(new LogMessage(logLevel, eventId, state, exception, typeof(TState)));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= MinLogLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<LogMessage> RecordedLogMessages => _recordedLogMessages;
        public LogLevel MinLogLevel { get; set; }

        public readonly struct LogMessage
        {
            public LogMessage(LogLevel logLevel, EventId eventId, object state, Exception exception, Type stateType)
            {
                LogLevel = logLevel;
                EventId = eventId;
                State = state;
                Exception = exception;
                StateType = stateType;
            }

            public LogLevel LogLevel { get; }
            public EventId EventId { get; }
            public object State { get; }
            public Exception Exception { get; }
            public Type StateType { get; }
        }
    }
}
