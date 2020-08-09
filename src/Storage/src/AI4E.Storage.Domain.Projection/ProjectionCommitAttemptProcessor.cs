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

using System;

namespace AI4E.Storage.Domain.Projection
{
    public sealed class ProjectionCommitAttemptProcessor : CommitAttemptProcessorBase<CommitAttemptEntry>
    {
        protected override void ProcessEntry<TCommitAttemptEntry>(
            in TCommitAttemptEntry source, out CommitAttemptEntry dest)
        {
            using var resultBuilder = CommitAttemptEntry.CreateBuilder(source);

            var projectDomainEvent = new DomainEvent(
                new ProjectEntityMessage(source.EntityIdentifier.EntityType, source.EntityIdentifier.EntityId));

            resultBuilder.DomainEvents = resultBuilder.DomainEvents.Add(projectDomainEvent);
            resultBuilder.Build(out dest);
        }

        public static IProjectionBuilder Register(IProjectionBuilder projectionBuilder)
        {
            if (projectionBuilder is null)
                throw new ArgumentNullException(nameof(projectionBuilder));

            static void Configure(ICommitAttemptProcessorRegistry registry)
            {
                registry.Register(CommitAttemptProcessorRegistration.Create<ProjectionCommitAttemptProcessor>());
            }

            projectionBuilder.DomainStorageBuilder.ConfigureCommitAttemptProccessors(Configure);

            return projectionBuilder;
        }
    }
}
