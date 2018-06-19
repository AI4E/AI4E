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
using System.Linq;
using AI4E.Storage.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage
{
    public static class StorageBuilderExtension
    {
        public static IStorageBuilder UseDatabase<TDatabase>(this IStorageBuilder builder)
            where TDatabase : class, IDatabase
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var services = builder.Services;

            services.AddSingleton<IDatabase, TDatabase>();
            services.AddSingleton(p => p.GetRequiredService<IDatabase>() as IFilterableDatabase);
            services.AddSingleton(p => p.GetRequiredService<IDatabase>() as IQueryableDatabase);

            services.UseTransactionSubsystem();

            return builder;
        }

        public static IStorageBuilder UseDatabase<TDatabase>(this IStorageBuilder builder, Func<IServiceProvider, TDatabase> factory)
            where TDatabase : class, IDatabase
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var services = builder.Services;

            services.AddSingleton<IDatabase, TDatabase>(factory);
            services.AddSingleton(p => p.GetRequiredService<IDatabase>() as IFilterableDatabase);
            services.AddSingleton(p => p.GetRequiredService<IDatabase>() as IQueryableDatabase);

            services.UseTransactionSubsystem();

            return builder;
        }

        public static IStorageBuilder UseTransactionalDatabase<TDatabase>(this IStorageBuilder builder)
            where TDatabase : class, ITransactionalDatabase
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var services = builder.Services;

            services.AddSingleton<ITransactionalDatabase, TDatabase>();
            services.AddSingleton(p => p.GetRequiredService<ITransactionalDatabase>() as IQueryableTransactionalDatabase);

            // TODO: Provide a wrapper for IDatabase, IFilterableDatabase, IQueryableDatabase

            return builder;
        }

        public static IStorageBuilder UseTransactionalDatabase<TDatabase>(this IStorageBuilder builder, Func<IServiceProvider, TDatabase> factory)
            where TDatabase : class, ITransactionalDatabase
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var services = builder.Services;

            services.AddSingleton<ITransactionalDatabase, TDatabase>(factory);
            services.AddSingleton(p => p.GetRequiredService<ITransactionalDatabase>() as IQueryableTransactionalDatabase);

            // TODO: Provide a wrapper for IDatabase, IFilterableDatabase, IQueryableDatabase

            return builder;
        }

        private static void UseTransactionSubsystem(this IServiceCollection services)
        {
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<IEntryStateTransformerFactory, EntryStateTransformerFactory>();
            services.AddSingleton<IEntryStateStorageFactory, EntryStateStorageFactory>();
            services.AddSingleton<ITransactionStateTransformer, TransactionStateTransformer>();
            services.AddSingleton<ITransactionStateStorage, TransactionStateStorage>();
            services.AddSingleton<ITransactionManager, TransactionManager>();
            services.AddSingleton<ITransactionalDatabase, TransactionalDatabase>();
            // TODO: Register IQueryablTransactionalDatabase if the underlying non-transactional database is queryable.
        }

        public static IStorageBuilder Configure(this IStorageBuilder builder, Action<StorageOptions> configuration)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            builder.Services.Configure(configuration);

            return builder;
        }
    }
}
