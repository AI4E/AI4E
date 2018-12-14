/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        ValidationResult.cs 
 * Types:           (1) AI4E.ValidationResult
 *                  (2) AI4E.ValidationResultsBuilder
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   25.02.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

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
using Newtonsoft.Json;

namespace AI4E
{
    /// <summary>
    /// Represents a validation result.
    /// </summary>
    public readonly struct ValidationResult : IEquatable<ValidationResult>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ValidationResult"/> type.
        /// </summary>
        /// <param name="member">The member whose validation failed.</param>
        /// <param name="message">A message describing the validation failure.</param>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="member"/> or <paramref name="message"/> is null.</exception>
        [JsonConstructor]
        public ValidationResult(string member, string message)
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            Member = member;
            Message = message;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ValidationResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the validation failure.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is null.</exception>
        public ValidationResult(string message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            Member = null;
            Message = message;
        }

        /// <summary>
        /// Gets the member whose validation failed or null if no member is specified.
        /// </summary>
        public string Member { get; }

        /// <summary>
        /// Gets a message describing the validation failure.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Returns a boolean value indicating whether the specified object equals the current valition result.
        /// </summary>
        /// <param name="obj">The object that shall be compared the the current validation result.</param>
        /// <returns>True if <paramref name="obj"/> is a <see cref="ValidationResult"/> and equals the current one, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is ValidationResult validationResult && validationResult.Equals(this);
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified <see cref="ValidationResult"/> equals the current one.
        /// </summary>
        /// <param name="other">The <see cref="ValidationResult"/> that shall be compared to the current one.</param>
        /// <returns>True, if <paramref name="other"/> equals the current validation result, false otherwise.</returns>
        public bool Equals(ValidationResult other)
        {
            return Member == other.Member &&
                   Message == other.Message;
        }

        /// <summary>
        /// Returns a hash code for the current validation result.
        /// </summary>
        /// <returns>A hash code for the current validation result.</returns>
        public override int GetHashCode()
        {
            if (Member == null)
                return Message.GetHashCode();

            return Member.GetHashCode() ^ Message.GetHashCode();
        }

        /// <summary>
        /// Returns a string representing the current validation result.
        /// </summary>
        /// <returns>A string representing the current validation result.</returns>
        public override string ToString()
        {
            return $"{Member} : {Message}";
        }

        /// <summary>
        /// Returns a boolean value indication whether two <see cref="ValidationResult"/>s are equal.
        /// </summary>
        /// <param name="left">The first validation result.</param>
        /// <param name="right">The second validation result.</param>
        /// <returns>True, if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(ValidationResult left, ValidationResult right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indication whether two <see cref="ValidationResult"/>s are inequal.
        /// </summary>
        /// <param name="left">The first validation result.</param>
        /// <param name="right">The second validation result.</param>
        /// <returns>True, if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(ValidationResult left, ValidationResult right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// A builder for validation results.
    /// </summary>
    public sealed class ValidationResultsBuilder
    {
        private readonly HashSet<ValidationResult> _validationResults = new HashSet<ValidationResult>();

        /// <summary>
        /// Creates a new instance of the <see cref="ValidationResultsBuilder"/> type.
        /// </summary>
        public ValidationResultsBuilder() { }

        /// <summary>
        /// Adds a validation result.
        /// </summary>
        /// <param name="member">The member whose validation failed.</param>
        /// <param name="message">A message describing the validation failure.</param>
        public void AddValidationResult(string member, string message)
        {
            _validationResults.Add(new ValidationResult(member, message));
        }

        public void AddValidationResult(string message)
        {
            _validationResults.Add(new ValidationResult(message));
        }

        /// <summary>
        /// Returns the validation results collection.
        /// </summary>
        /// <returns>A collection of validation results.</returns>
        public IEnumerable<ValidationResult> GetValidationResults()
        {
            return _validationResults;
        }
    }

    public static class ValidationResultsBuilderExtension
    {
        public static void Validate<T>(this ValidationResultsBuilder validationResultsBuilder,
                                       ValidationFunction<T> validationFunction,
                                       T value,
                                       string member)
        {
            if (validationResultsBuilder == null)
                throw new ArgumentNullException(nameof(validationResultsBuilder));

            if (validationFunction == null)
                throw new ArgumentNullException(nameof(validationFunction));

            if (!validationFunction(value, out var message))
            {
                validationResultsBuilder.AddValidationResult(member, message);
            }
        }

        public delegate bool ValidationFunction<T>(T value, out string message);
    }
}
