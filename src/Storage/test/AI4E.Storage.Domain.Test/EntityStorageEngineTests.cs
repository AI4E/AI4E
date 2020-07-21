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
using AI4E.Storage.Domain.Specification;
using AI4E.Storage.MongoDB;
using AI4E.Storage.MongoDB.Test.Utils;
using AutoFixture;
using AutoFixture.AutoMoq;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public sealed class EntityStorageEngineTests : EntityStorageEngineSpecification
    {
        private readonly MongoClient _databaseClient = DatabaseRunner.CreateClient();
        private readonly Lazy<IDatabase> _database;

        public EntityStorageEngineTests()
        {
            _database = new Lazy<IDatabase>(BuildDatabase);
        }

        private IFixture Fixture { get; } = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });

        private IDatabase BuildDatabase()
        {
            var wrappedDatabase = _databaseClient.GetDatabase(DatabaseName.GenerateRandom());
            return new MongoDatabase(wrappedDatabase);
        }

        protected override IEntityStorageEngine Create(IDomainEventDispatcher eventDispatcher)
        {
            var database = _database.Value;
            var optionsAccessor = Options.Create(new DomainStorageOptions { WaitForEventsDispatch = true });

            return new EntityStorageEngine(database, eventDispatcher, optionsAccessor);
        }

        [Fact]
        public void CtorNullDatabaseThrowsArgumentNullExceptionTest()
        {
            // Arrange
            // -

            // Act
            void Act()
            {
                new EntityStorageEngine(
                    database: null,
                    Fixture.Create<IDomainEventDispatcher>(),
                    Fixture.Create<IOptions<DomainStorageOptions>>());
            }

            // Assert
            Assert.Throws<ArgumentNullException>("database", Act);
        }

        [Fact]
        public void CtorNullEventDispatcherThrowsArgumentNullExceptionTest()
        {
            // Arrange
            // -

            // Act
            void Act()
            {
                new EntityStorageEngine(
                    Fixture.Create<IDatabase>(),
                    eventDispatcher: null,
                    Fixture.Create<IOptions<DomainStorageOptions>>());
            }

            // Assert
            Assert.Throws<ArgumentNullException>("eventDispatcher", Act);
        }

        [Fact]
        public void CtorNullOptionsAccessorThrowsArgumentNullExceptionTest()
        {
            // Arrange
            // -

            // Act
            void Act()
            {
                new EntityStorageEngine(
                    Fixture.Create<IDatabase>(),
                    Fixture.Create<IDomainEventDispatcher>(),
                    optionsAccessor: null);
            }

            // Assert
            Assert.Throws<ArgumentNullException>("optionsAccessor", Act);
        }
    }
}
