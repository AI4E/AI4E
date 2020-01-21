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
using System.Runtime.Serialization;

namespace AI4E.Messaging.Validation
{
    /// <summary>
    /// Describes the result of a message dispatch operation thats validation failed.
    /// </summary>
    [Serializable]
    public class ValidationFailureDispatchResult : FailureDispatchResult
    {
        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="ValidationFailureDispatchResult"/> type.
        /// </summary>
        public ValidationFailureDispatchResult() : base("Validation failure")
        {
            ValidationResults = ImmutableList<ValidationResult>.Empty;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ValidationFailureDispatchResult"/> type.
        /// </summary>
        /// <param name="validationResults">
        /// An enumerable of <see cref="ValidationResult"/>s that describe the failed validation.
        /// </param>
        public ValidationFailureDispatchResult(IEnumerable<ValidationResult> validationResults) : this()
        {
            if (validationResults == null)
                throw new ArgumentNullException(nameof(validationResults));

            ValidationResults = validationResults.ToImmutableList();
        }

        #endregion

        #region ISerializable

        protected ValidationFailureDispatchResult(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
            ImmutableList<ValidationResult>? validationResults;

            try
            {
#pragma warning disable CA1062
                validationResults = serializationInfo.GetValue(nameof(ValidationResults), typeof(ImmutableList<ValidationResult>))
                    as ImmutableList<ValidationResult>;
#pragma warning restore CA1062
            }
            catch (InvalidCastException exc)
            {
                // TODO: More specific error message
                throw new SerializationException("Cannot deserialize dispatch result.", exc);
            }

            if (validationResults is null)
            {
                // TODO: More specific error message
                throw new SerializationException("Cannot deserialize dispatch result.");
            }

            ValidationResults = validationResults;
        }

        protected override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
#pragma warning disable CA1062
            info.AddValue(nameof(ValidationResults), ValidationResults, typeof(ImmutableList<ValidationResult>));
#pragma warning restore CA1062
        }

        #endregion

        /// <summary>
        /// Gets an enumerable of <see cref="ValidationResult"/>s that describe the failed validation.
        /// </summary>
        public ImmutableList<ValidationResult> ValidationResults { get; }
    }
}
