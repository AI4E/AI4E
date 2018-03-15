/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        DomainEvent.cs 
 * Types:           (1) AI4E.Domain.DomainEvent
 *                  (2) AI4E.Domain.DomainEvent'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   15.03.2018 
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

namespace AI4E.Domain
{
    public abstract class DomainEvent
    {
        protected DomainEvent(Guid id)
        {
            if (id == default)
                throw new ArgumentException("The id must not be an empty guid.", nameof(id));

            Id = id;
        }

        public Guid Id { get; }
    }
}