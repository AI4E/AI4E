/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    public class FailureDispatchResult : DispatchResult
    {
        [JsonConstructor]
        private FailureDispatchResult(string message, Exception exception, IReadOnlyDictionary<string, object> resultData)
                    : base(false, exception != null ? FormatMessage(exception) : message, resultData)
        { }

        public FailureDispatchResult(string message, IReadOnlyDictionary<string, object> resultData)
            : base(false, message, resultData)
        { }

        public FailureDispatchResult(string message)
            : base(false, message, ImmutableDictionary<string, object>.Empty)
        { }

        public FailureDispatchResult(Exception exception) : this(FormatMessage(exception))
        {
            Exception = exception;
        }

        private static string FormatMessage(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return "An unhandled exception occured: " + exception.Message;
        }

        public FailureDispatchResult() : this("Unknown failure.") { }

        public Exception Exception { get; }

        [JsonIgnore]
        public override bool IsSuccess => false;
    }
}
