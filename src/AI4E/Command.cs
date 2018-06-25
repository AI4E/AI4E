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
using AI4E.Internal;

namespace AI4E
{
    public abstract class Command<TId>
    {
        protected Command(TId id)
        {
            Id = id;
        }

        public TId Id { get; }
    }

    public abstract class Command : Command<Guid>
    {
        protected Command(Guid id) : base(id) { }
    }

    public abstract class ConcurrencySafeCommand<TId> : Command<TId>
    {
        protected ConcurrencySafeCommand(TId id, string concurrencyToken) : base(id)
        {
            ConcurrencyToken = concurrencyToken;
        }

        public string ConcurrencyToken { get; }
    }

    public abstract class ConcurrencySafeCommand : ConcurrencySafeCommand<Guid>
    {
        protected ConcurrencySafeCommand(Guid id, string concurrencyToken) : base(id, concurrencyToken) { }
    }
}
