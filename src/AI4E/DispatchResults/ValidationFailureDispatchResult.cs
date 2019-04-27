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
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    /// <summary>
    /// Describes the result of a message dispatch operation thats validation failed.
    /// </summary>
    public class ValidationFailureDispatchResult : FailureDispatchResult
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ValidationFailureDispatchResult"/> type.
        /// </summary>
        public ValidationFailureDispatchResult() : base("Validation failure") { }

        /// <summary>
        /// Creates a new instance of the <see cref="ValidationFailureDispatchResult"/> type.
        /// </summary>
        /// <param name="validationResults">
        /// An enumerable of <see cref="ValidationResult"/>s that describe the failed validation.
        /// </param>
        [JsonConstructor]
        public ValidationFailureDispatchResult(IEnumerable<ValidationResult> validationResults) : this()
        {
            if (validationResults == null)
                throw new ArgumentNullException(nameof(validationResults));

            ValidationResults = validationResults.ToImmutableList();
        }

        /// <summary>
        /// Gets an enumerable of <see cref="ValidationResult"/>s that describe the failed validation.
        /// </summary>
        public ImmutableList<ValidationResult> ValidationResults { get; }
    }
}
