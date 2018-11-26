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
using System.Text;
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    public class SuccessDispatchResult : DispatchResult
    {
        [JsonConstructor]
        public SuccessDispatchResult(string message, IReadOnlyDictionary<string, object> resultData)
            : base(true, message, resultData) { }

        public SuccessDispatchResult(string message)
            : this(message, ImmutableDictionary<string, object>.Empty) { }

        public SuccessDispatchResult()
            : this("Success") { }

        [JsonIgnore]
        public override bool IsSuccess => true;
    }

    public class SuccessDispatchResult<TResult> : SuccessDispatchResult, IDispatchResult<TResult>
    {
        [JsonConstructor]
        public SuccessDispatchResult(TResult result, string message, IReadOnlyDictionary<string, object> resultData) : base(message, resultData)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Result = result;
        }

        public SuccessDispatchResult(TResult result, string message) : base(message)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Result = result;
        }

        public SuccessDispatchResult(TResult result) : this(result, "Success") { }

        public TResult Result { get; }

        protected override void FormatString(StringBuilder stringBuilder)
        {
            base.FormatString(stringBuilder);

            stringBuilder.Append("[Result: ");
            stringBuilder.Append(Result.ToString());
            stringBuilder.Append("]");
        }
    }
}
