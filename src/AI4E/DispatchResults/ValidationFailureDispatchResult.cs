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

using System.Collections.Generic;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    public class ValidationFailureDispatchResult : FailureDispatchResult
    {
        public ValidationFailureDispatchResult() : base("Validation failure") { }

        [JsonConstructor]
        public ValidationFailureDispatchResult(IEnumerable<ValidationResult> validationResults) : this()
        {
            ValidationResults = validationResults.ToImmutableList();
        }

        public ImmutableList<ValidationResult> ValidationResults { get; }
    }
}
