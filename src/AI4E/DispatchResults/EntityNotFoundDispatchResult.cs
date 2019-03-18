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
using System.Text;
using AI4E.Utils;
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    public class EntityNotFoundDispatchResult : NotFoundDispatchResult
    {
        public EntityNotFoundDispatchResult(Type entityType, string id) : this(entityType?.GetUnqualifiedTypeName(), id) { }

        public EntityNotFoundDispatchResult(Type entityType) : this(entityType?.GetUnqualifiedTypeName(), id: null) { }

        [JsonConstructor]
        public EntityNotFoundDispatchResult(string entityTypeName, string id) : base(BuildMessage(entityTypeName, id: null))
        {
            EntityTypeName = entityTypeName;
            Id = id;
        }

        private const string _messagePart1 = "The entity of type '";
        private const string _messagePart2A = "' with the specified id was not found.";
        private const string _messagePart2B = "' with the id '";
        private const string _messagePart3 = "' was not found.";

        private static string BuildMessage(string entityTypeName, string id)
        {
            if (entityTypeName == null)
                throw new ArgumentNullException(nameof(entityTypeName));

            StringBuilder resultBuilder;

            if (id == null)
            {
                resultBuilder = new StringBuilder(_messagePart1.Length + _messagePart2A.Length + entityTypeName.Length);
                resultBuilder.Append(_messagePart1);
                resultBuilder.Append(entityTypeName);
                resultBuilder.Append(_messagePart2A);
            }
            else
            {
                resultBuilder = new StringBuilder(_messagePart1.Length + _messagePart2B.Length + _messagePart3.Length + entityTypeName.Length + id.Length);
                resultBuilder.Append(_messagePart1);
                resultBuilder.Append(entityTypeName);
                resultBuilder.Append(_messagePart2B);
                resultBuilder.Append(id);
                resultBuilder.Append(_messagePart3);
            }

            return resultBuilder.ToString();
        }

        public string EntityTypeName { get; }

        public string Id { get; }

        public bool TryGetEntityType(out Type entityType)
        {
            entityType = TypeLoadHelper.LoadTypeFromUnqualifiedName(EntityTypeName, throwIfNotFound: false);
            return entityType != null;
        }
    }
}
