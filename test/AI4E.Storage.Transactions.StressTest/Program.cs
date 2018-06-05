﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.Storage.Transactions.StressTest
{
    class Program
    {
        private static int _ba1AmountComparand = 0;
        private static int _ba2AmountComparand = 1;

        [ThreadStatic]
        private static Random _rnd;
        private static int _count = 0;

        private static Random Rnd
        {
            get
            {
                if (_rnd == null)
                {
                    var seed = GetNextSeed();

                    _rnd = new Random(seed);
                }

                return _rnd;
            }
        }

        private static int GetNextSeed()
        {
            var count = Interlocked.Increment(ref _count);

            unchecked
            {
                return count + Environment.TickCount;
            }
        }

        public static async Task Main(string[] args)
        {

            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            long bankAccountNo1, bankAccountNo2;

            {
                var transactionalStore = serviceProvider.GetRequiredService<ITransactionalDatabase>();

                (bankAccountNo1, bankAccountNo2) = await CreateBankAccountsAsync(transactionalStore);
            }

            var tasks = new List<Task>();

            for (var i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(() => TransferAsync(bankAccountNo1, bankAccountNo2, serviceProvider.GetRequiredService<ITransactionManager>())));
            }

            tasks.Add(Task.Run(async () =>
            {
                var numTasks = tasks.Count - 1;

                var completedBefore = 0;

                for (var completed = tasks.Where(p => p.IsCompleted).Count();
                     completed < tasks.Count - 1;
                     completed = tasks.Where(p => p.IsCompleted).Count())
                {
                    if (completed != completedBefore)
                    {
                        await Console.Out.WriteLineAsync($"{completed} of {numTasks} tasks completed ({((double)completed / (double)numTasks * 100d).ToString("0.00")}%)");
                    }

                    completedBefore = completed;

                    await Task.Delay(200);
                }

                await Console.Out.WriteLineAsync($"{numTasks} of {numTasks} tasks completed (100%)");
            }));

            await Task.WhenAll(tasks);

            {
                var transactionalStore = serviceProvider.GetRequiredService<ITransactionalDatabase>();

                await OutputAsync(bankAccountNo1, bankAccountNo2, transactionalStore);
            }

            await Console.In.ReadLineAsync();
        }

        private static async Task OutputAsync(long bankAccountNo1, long bankAccountNo2, ITransactionalDatabase transactionalStore)
        {
            BankAccount bankAccount1, bankAccount2;

            try
            {
                bankAccount1 = (await transactionalStore.GetAsync<BankAccount>(p => p.Id == bankAccountNo1)).FirstOrDefault();
                bankAccount2 = (await transactionalStore.GetAsync<BankAccount>(p => p.Id == bankAccountNo2)).FirstOrDefault();

                await transactionalStore.TryCommitAsync();
            }
            catch
            {
                await transactionalStore.RollbackAsync();
                throw;
            }

            await Console.Out.WriteLineAsync($"Account1 amount: {bankAccount1.Amount} should be {_ba1AmountComparand}");
            await Console.Out.WriteLineAsync($"Account1 amount: {bankAccount2.Amount} should be {_ba2AmountComparand}");

            await Console.Out.WriteLineAsync();

            await Console.Out.WriteLineAsync(bankAccount1.Amount == _ba1AmountComparand && bankAccount2.Amount == _ba2AmountComparand ? "OK" : "NOT OK");
        }

        private static async Task TransferAsync(long bankAccountNo1, long bankAccountNo2, ITransactionManager transactionalManager)
        {
            var transferAmount = Rnd.Next(2001) - 1000;

            ITransactionalDatabase transactionalStore;

            do
            {
                transactionalStore = transactionalManager.CreateStore();

                var bankAccount1 = (await transactionalStore.GetAsync<BankAccount>(p => p.Id == bankAccountNo1)).FirstOrDefault();
                var bankAccount2 = (await transactionalStore.GetAsync<BankAccount>(p => p.Id == bankAccountNo2)).FirstOrDefault();

                bankAccount1.Amount -= transferAmount;
                bankAccount2.Amount += transferAmount;

                await transactionalStore.StoreAsync(bankAccount1);
                await transactionalStore.StoreAsync(bankAccount2);
            }
            while (!await transactionalStore.TryCommitAsync());

            Interlocked.Add(ref _ba1AmountComparand, -transferAmount);
            Interlocked.Add(ref _ba2AmountComparand, transferAmount);
        }

        private static async Task<(long bankAccountNo1, long bankAccountNo2)> CreateBankAccountsAsync(ITransactionalDatabase transactionalStore)
        {
            _ba1AmountComparand = Rnd.Next();
            _ba2AmountComparand = Rnd.Next();

            var bankAccount1 = new BankAccount { Id = 1, Amount = _ba1AmountComparand };
            var bankAccount2 = new BankAccount { Id = 2, Amount = _ba2AmountComparand };

            try
            {
                await transactionalStore.StoreAsync(bankAccount1);
                await transactionalStore.StoreAsync(bankAccount2);

                await transactionalStore.TryCommitAsync();
            }
            catch
            {
                await transactionalStore.RollbackAsync();
                throw;
            }

            return (bankAccount1.Id, bankAccount2.Id);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IFilterableDatabase, InMemoryDatabase>();
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<IEntryStateTransformerFactory, EntryStateTransformerFactory>();
            services.AddSingleton<IEntryStorageFactory, EntryStorageFactory>();
            services.AddSingleton<ITransactionStateTransformer, TransactionStateTransformer>();
            services.AddSingleton<ITransactionStateStorage, TransactionStateStorage>();
            services.AddSingleton<ITransactionManager, TransactionManager>();
            services.AddTransient(provider => provider.GetRequiredService<ITransactionManager>().CreateStore());

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                //builder.AddFile(new FileLoggerContext(AppContext.BaseDirectory, "transactionTest.log"));
                builder.AddConsole();
            });
        }
    }

    public sealed class BankAccount
    {
        public long Id { get; set; }
        public int Amount { get; set; }
    }
}