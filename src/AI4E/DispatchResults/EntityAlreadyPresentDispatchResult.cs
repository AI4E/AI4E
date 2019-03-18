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
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    public class EntityAlreadyPresentDispatchResult : FailureDispatchResult
    {
        [JsonConstructor]
        public EntityAlreadyPresentDispatchResult(Type entityType, string id)
            : base($"An entity of type'{(entityType ?? throw new ArgumentNullException(nameof(entityType))).FullName}' with the id '{id}' is already present.")
        {
            EntityType = entityType;
            Id = id;
        }

        public EntityAlreadyPresentDispatchResult(Type entityType)
            : base($"An entity of type'{(entityType ?? throw new ArgumentNullException(nameof(entityType))).FullName}' with the specified id is already present.")
        {
            EntityType = entityType;
        }

        public Type EntityType { get; }

        public string Id { get; }
    }
}
