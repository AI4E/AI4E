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

namespace AI4E
{
    /// <summary>
    /// Describes a message processor dependencies.
    /// </summary>
    public readonly struct MessageProcessorDependency : IEquatable<MessageProcessorDependency>
    {
        private readonly Func<IMessageProcessorRegistration, bool> _predicate;

        /// <summary>
        /// Creates a new instance of the <see cref="MessageProcessorDependency"/> type.
        /// </summary>
        /// <param name="predicate">
        /// A predicate that returns <c>true</c> if the specified message processor
        /// is a dependency of the current message process.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="predicate"/> is <c>null</c>.
        /// </exception>
        public MessageProcessorDependency(Func<IMessageProcessorRegistration, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _predicate = predicate;
        }

        /// <summary>
        /// Returns a boolean value indicating whether the sepcified message processor
        /// is a dependency of the current message processor.
        /// </summary>
        /// <param name="messageProcessorRegistration">The message processor.</param>
        /// <returns>
        /// True if <paramref name="messageProcessorRegistration"/> is a dependency
        /// of the current message processor, false otherwise.
        /// </returns>
        public bool IsDependency(IMessageProcessorRegistration messageProcessorRegistration)
        {
            if (messageProcessorRegistration == null)
                throw new ArgumentNullException(nameof(messageProcessorRegistration));

            return _predicate?.Invoke(messageProcessorRegistration) ?? false;
        }

        /// <inheritdoc/>
        public bool Equals(MessageProcessorDependency other)
        {
            return other._predicate == _predicate;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is MessageProcessorDependency messageProcessorDependency &&
                Equals(messageProcessorDependency);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return _predicate?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Compares two <see cref="MessageProcessorDependency"/> values.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>
        /// True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.
        /// </returns>
        public static bool operator ==(MessageProcessorDependency left, MessageProcessorDependency right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="MessageProcessorDependency"/> values.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>
        /// True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.
        /// </returns>
        public static bool operator !=(MessageProcessorDependency left, MessageProcessorDependency right)
        {
            return !left.Equals(right);
        }
    }
}
