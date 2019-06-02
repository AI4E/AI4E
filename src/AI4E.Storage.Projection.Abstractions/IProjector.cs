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
using System.Collections.Generic;
using System.Threading;

namespace AI4E.Storage.Projection
{
    // TODO: Rename to ProjectionExecutor and ProjectAsync to ExecuteProjectionAsync?
    public interface IProjector
    {
        IProjectionProvider ProjectionProvider { get; }

        IAsyncEnumerable<IProjectionResult> ProjectAsync(
            Type sourceType,
            object source,
            IServiceProvider serviceProvider,
            CancellationToken cancellation);
    }

    public static class ProjectorExtension
    {
        public static IAsyncEnumerable<IProjectionResult> ProjectAsync<TSource>(
            this IProjector projector,
            TSource source,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
            where TSource : class
        {
            return projector.ProjectAsync(typeof(TSource), source, serviceProvider, cancellation);
        }
    }
}
