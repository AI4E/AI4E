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

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AI4E.Storage
{
    public class StorageBuilder : IStorageBuilder
    {
        private protected StorageBuilder(IServiceCollection services)
        {
            Debug.Assert(services != null);
            Services = services!;
        }

        public StorageBuilder()
        {
            Services = new ServiceCollection();
        }

        public IServiceCollection Services { get; }

        public IDatabase Build()
        {
            var serviceProvider = Services.BuildServiceProvider();
            return serviceProvider.GetRequiredService<IDatabase>();
        }
    }

    internal sealed class StorageBuilderImpl : StorageBuilder
    {
        public StorageBuilderImpl(IServiceCollection services) : base(services) { }
    }
}
