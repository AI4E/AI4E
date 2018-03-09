/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IReferenceResolver.cs 
 * Types:           (1) AI4E.Domain.IReferenceResolver
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   18.10.2017 
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
    public interface IReferenceResolver
    {
        Task<TEntity> ResolveAsync<TEntity>(Guid id, long revision, CancellationToken cancellation)
                   where TEntity : AggregateRoot;
    }

    public static class ReferenceResolverExtension
    {
        public static Task<TEntity> ResolveAsync<TEntity>(this IReferenceResolver referenceResolver, Guid id, CancellationToken cancellation)
            where TEntity : AggregateRoot
        {
            if (referenceResolver == null)
                throw new ArgumentNullException(nameof(referenceResolver));

            return referenceResolver.ResolveAsync<TEntity>(id, revision: default, cancellation);
        }
    }
}
