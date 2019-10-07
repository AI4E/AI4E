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
using AI4E.Storage.InMemory;

namespace AI4E.Storage
{
    /// <summary>
    /// Contains extensions for the <see cref="IStorageBuilder"/> type.
    /// </summary>
    public static class InMemoryDatabaseStorageBuilderExtension
    {
        /// <summary>
        /// Uses an in-memory database.
        /// </summary>
        /// <param name="builder">The storage builder.</param>
        /// <returns>The storage builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> is <c>null</c>.</exception>
        public static IStorageBuilder UseInMemoryDatabase(this IStorageBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.UseDatabase<InMemoryDatabase>();

            return builder;
        }
    }
}
