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
    public readonly struct MessageProcessorDependency : IEquatable<MessageProcessorDependency>
    {
        private readonly Func<IMessageProcessorRegistration, bool> _predicate;

        public MessageProcessorDependency(Func<IMessageProcessorRegistration, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _predicate = predicate;
        }

        public bool IsDependency(IMessageProcessorRegistration messageProcessorRegistration)
        {
            if (messageProcessorRegistration == null)
                throw new ArgumentNullException(nameof(messageProcessorRegistration));

            return _predicate?.Invoke(messageProcessorRegistration) ?? false;
        }

        public bool Equals(MessageProcessorDependency other)
        {
            return other._predicate == _predicate;
        }

        public override bool Equals(object obj)
        {
            return obj is MessageProcessorDependency messageProcessorDependency &&
                Equals(messageProcessorDependency);
        }

        public override int GetHashCode()
        {
            return _predicate?.GetHashCode() ?? 0;
        }

        public static bool operator ==(MessageProcessorDependency left, MessageProcessorDependency right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MessageProcessorDependency left, MessageProcessorDependency right)
        {
            return !left.Equals(right);
        }
    }
}
