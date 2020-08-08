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

using AI4E.Storage.Domain.Specification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AI4E.Storage.Domain.Test
{
    public sealed class EntityStorageTests : EntityStorageSpecification
    {
        protected override IEntityStorage Create(
            IEntityStorageEngine storageEngine,
            IEntityMetadataManager metadataManager)
        {
            var idFactory = new EntityIdFactory();
            var concurrencyTokenFactory = new ConcurrencyTokenFactory();
            var optionsAccessor = Options.Create(new DomainStorageOptions { });

            return new EntityStorage(
                storageEngine,
                metadataManager,
                idFactory,
                concurrencyTokenFactory,
                new CommitAttemptProcessingQueue(),
                new ServiceCollection().BuildServiceProvider(),
                optionsAccessor);
        }
    }
}
