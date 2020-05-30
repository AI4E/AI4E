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
using AI4E.Storage.Test.Dummies;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed class ServiceCollectionExtensionTests
    {
        private IServiceProvider ConfigureServices(Action<IServiceCollection> configuration)
        {
            var services = new ServiceCollection();
            configuration(services);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void AddStorageNullServicesThrowsNullReferenceException()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                ServiceCollectionExtension.AddStorage(services: null);
            });
        }

        [Fact]
        public void AddStorageAddsNullDatabaseTest()
        {
            var serviceProvider = ConfigureServices(services => services.AddStorage());
            var database = serviceProvider.GetRequiredService<IDatabase>();

            Assert.IsType<NoDatabase>(database);
        }

        [Fact]
        public void AddStorageDoesNotOverrideRegisteredDatabaseTest()
        {
            var serviceProvider = ConfigureServices(services =>
            {
                services.AddSingleton<IDatabase, DatabaseDummy>();
                services.AddStorage();
            });

            var database = serviceProvider.GetRequiredService<IDatabase>();

            Assert.IsType<DatabaseDummy>(database);
        }

        [Fact]
        public void AddStorageReturnsValidStorageBuilderTest()
        {
            var services = new ServiceCollection();
            var storageBuilder = services.AddStorage();

            Assert.NotNull(storageBuilder);
            Assert.Same(services, storageBuilder.Services);
        }

        [Fact]
        public void AddStorageSubsequentCallReturnsSameStorageBuilderTest()
        {
            var services = new ServiceCollection();
            var expectedStorageBuilder = services.AddStorage();
            var testStorageBuilder = services.AddStorage();

            Assert.Same(expectedStorageBuilder, testStorageBuilder);
        }
    }
}
