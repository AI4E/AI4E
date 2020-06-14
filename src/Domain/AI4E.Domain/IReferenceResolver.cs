/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IReferenceResolver.cs 
 * Types:           (1) AI4E.Domain.IReferenceResolver
 *                  (2) AI4E.Domain.ReferenceResolverExtension
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   18.03.2018 
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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Domain
{
    /// <summary>
    /// Represents a reference resolver that can resolve references asynchronously.
    /// </summary>
    public interface IReferenceResolver
    {
        /// <summary>
        /// Asynchronously resolves the reference specified by its type, id and revision.
        /// </summary>
        /// <typeparam name="TEntity">The type of entity the reference refers to.</typeparam>
        /// <param name="id">The id of the referenced entity.</param>
        /// <param name="revision">The revision of the entity to load or <c>default</c> to load the entity in the latest version.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the loaded entity or null if the reference could not be resolved.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="revision"/> is a negative value.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        ValueTask<TEntity> ResolveAsync<TEntity>(string id, long revision, CancellationToken cancellation = default)
                   where TEntity : class;
    }

    /// <summary>
    /// Provides common extension method for a reference resolver.
    /// </summary>
    public static class ReferenceResolverExtension
    {
        /// <summary>
        /// Asynchronously resolves the reference specified by its type and id within the latest revision.
        /// </summary>
        /// <typeparam name="TEntity">The type of entity the reference refers to.</typeparam>
        /// <param name="id">The id of the referenced entity.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the loaded entity or null if the reference could not be resolved.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="referenceResolver"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        public static ValueTask<TEntity> ResolveAsync<TEntity>(this IReferenceResolver referenceResolver, string id, CancellationToken cancellation = default)
            where TEntity : class
        {
            if (referenceResolver == null)
                throw new ArgumentNullException(nameof(referenceResolver));

            return referenceResolver.ResolveAsync<TEntity>(id, revision: default, cancellation);
        }
    }
}
