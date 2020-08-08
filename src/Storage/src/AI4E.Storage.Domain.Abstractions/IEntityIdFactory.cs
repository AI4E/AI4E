/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a factory for entity ids.
    /// </summary>
    public interface IEntityIdFactory
    {
        /// <summary>
        /// Creates an id for the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity an id shall be created for.</param>
        /// <returns>The created id.</returns>
        string CreateId(EntityDescriptor entityDescriptor);
    }
}