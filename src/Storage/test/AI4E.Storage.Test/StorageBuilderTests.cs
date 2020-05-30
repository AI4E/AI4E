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
using System.Linq;
using AI4E.Storage.Test.Dummies;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed class StorageBuilderTests
    {
        private StorageBuilder StorageBuilder { get; } = new StorageBuilder();

        [Fact]
        public void CtorTest()
        {
            Assert.NotNull(StorageBuilder.Services);
        }

        [Fact]
        public void BuildTest()
        {
            StorageBuilder.Services.AddSingleton<IDatabase, DatabaseDummy>();
            var database = StorageBuilder.Build();

            Assert.NotNull(database);
            Assert.IsType<DatabaseDummy>(database);
        }
    }

    public sealed class StorageBuilderExtensionTests
    {
        private StorageBuilder StorageBuilder { get; } = new StorageBuilder();

        [Fact]
        public void UseDatabaseNullBuilderThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                StorageBuilderExtension.UseDatabase<DatabaseDummy>(builder: null);
            });
        }

        [Fact]
        public void UseDatabaseFactoryNullBuilderThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                StorageBuilderExtension.UseDatabase(builder: null, provider => new DatabaseDummy());
            });
        }

        [Fact]
        public void UseDatabaseFactoryNullFactoryThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("factory", () =>
            {
                StorageBuilderExtension.UseDatabase<DatabaseDummy>(StorageBuilder, factory: null);
            });
        }

        [Fact]
        public void UseDatabaseRegistersDatabaseTest()
        {
            StorageBuilderExtension.UseDatabase<DatabaseDummy>(StorageBuilder);

            var serviceDescriptor = StorageBuilder.Services.LastOrDefault(p => p.ServiceType == typeof(IDatabase));

            Assert.NotNull(serviceDescriptor);
            Assert.Equal(typeof(DatabaseDummy), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
        }

        [Fact]
        public void UseDatabaseFactoryRegistersDatabaseTest()
        {
            Func<IServiceProvider, DatabaseDummy> factory = provider => new DatabaseDummy();
            StorageBuilderExtension.UseDatabase(StorageBuilder, factory);

            var serviceDescriptor = StorageBuilder.Services.LastOrDefault(p => p.ServiceType == typeof(IDatabase));

            Assert.NotNull(serviceDescriptor);
            Assert.Same(factory, serviceDescriptor.ImplementationFactory);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
        }
    }
}
