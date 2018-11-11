using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AI4E.Storage.Transactions.StressTest
{
    internal class Program
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
                var transactionalStore = serviceProvider.GetRequiredService<IScopedTransactionalDatabase>();

                (bankAccountNo1, bankAccountNo2) = await CreateBankAccountsAsync(transactionalStore);
            }

            var tasks = new List<Task>();

            for (var i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() => TransferAsync(bankAccountNo1, bankAccountNo2, serviceProvider.GetRequiredService<ITransactionalDatabase>())));
            }

            var consoleIn = Task.Run(() => Console.In.ReadLineAsync());
            tasks.Add(consoleIn);

            long count = 0;
            var watch = new Stopwatch();
            watch.Start();

            while (true)
            {
                var completed = await Task.WhenAny(tasks);

                if (completed == consoleIn)
                {
                    break;
                }

                tasks.Remove(completed);

                count++;

                await Console.Out.WriteLineAsync($"Task completed. Throughput: {(count * 1000.0) / watch.ElapsedMilliseconds }ops/sec");

                tasks.Add(Task.Run(() => TransferAsync(bankAccountNo1, bankAccountNo2, serviceProvider.GetRequiredService<ITransactionalDatabase>())));
            }

            await Console.Out.WriteLineAsync("Waiting for other ops to complete.");

            await Task.WhenAll(tasks);

            {
                var transactionalStore = serviceProvider.GetRequiredService<IScopedTransactionalDatabase>();

                await OutputAsync(bankAccountNo1, bankAccountNo2, transactionalStore);
            }

            await Console.In.ReadLineAsync();
        }

        private static async Task OutputAsync(long bankAccountNo1, long bankAccountNo2, IScopedTransactionalDatabase transactionalStore)
        {
            BankAccount bankAccount1, bankAccount2;

            try
            {
                bankAccount1 = await transactionalStore.GetAsync<BankAccount>(p => p.Id == bankAccountNo1).FirstOrDefault();
                bankAccount2 = await transactionalStore.GetAsync<BankAccount>(p => p.Id == bankAccountNo2).FirstOrDefault();

                await transactionalStore.TryCommitAsync();
            }
            catch
            {
                await transactionalStore.RollbackAsync();
                throw;
            }

            await Console.Out.WriteLineAsync($"Account1 amount: {bankAccount1.Amount} should be {_ba1AmountComparand}");
            await Console.Out.WriteLineAsync($"Account2 amount: {bankAccount2.Amount} should be {_ba2AmountComparand}");

            await Console.Out.WriteLineAsync();

            await Console.Out.WriteLineAsync(bankAccount1.Amount == _ba1AmountComparand && bankAccount2.Amount == _ba2AmountComparand ? "OK" : "NOT OK");
        }

        private static async Task TransferAsync(long bankAccountNo1, long bankAccountNo2, ITransactionalDatabase database)
        {
            var transferAmount = Rnd.Next(2001) - 1000;

            using (var transactionalStore = database.CreateScope())
            {
                do
                {
                    try
                    {
                        var bankAccount1 = await transactionalStore.GetAsync<BankAccount>(p => p.Id == bankAccountNo1).FirstOrDefault();
                        var bankAccount2 = await transactionalStore.GetAsync<BankAccount>(p => p.Id == bankAccountNo2).FirstOrDefault();

                        bankAccount1.Amount -= transferAmount;
                        bankAccount2.Amount += transferAmount;

                        await transactionalStore.StoreAsync(bankAccount1);
                        await transactionalStore.StoreAsync(bankAccount2);
                    }
                    catch (TransactionAbortedException)
                    {
                        continue;
                    }
                    catch
                    {
                        await transactionalStore.RollbackAsync();

                        throw;
                    }
                }
                while (!await transactionalStore.TryCommitAsync());
            }

            Interlocked.Add(ref _ba1AmountComparand, -transferAmount);
            Interlocked.Add(ref _ba2AmountComparand, transferAmount);
        }

        private static async Task<(long bankAccountNo1, long bankAccountNo2)> CreateBankAccountsAsync(IScopedTransactionalDatabase transactionalStore)
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
            services.AddStorage().UseMongoDB("AI4E-Transactions-DB", useNativeTransactions: true);

            //services.AddSingleton<IMongoClient>(provider => new MongoClient("mongodb://localhost:27017"));
            //services.AddSingleton(provider =>
            //    provider.GetRequiredService<IMongoClient>().GetDatabase("AI4E-Transactions-DB"));

            //services.AddSingleton<IFilterableDatabase, MongoDatabase>();

            //// services.AddSingleton<IFilterableDatabase, InMemoryDatabase>();
            //services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            //services.AddSingleton<IEntryStateTransformerFactory, EntryStateTransformerFactory>();
            //services.AddSingleton<IEntryStateStorageFactory, EntryStateStorageFactory>();
            //services.AddSingleton<ITransactionStateTransformer, TransactionStateTransformer>();
            //services.AddSingleton<ITransactionStateStorage, TransactionStateStorage>();
            //services.AddSingleton<ITransactionManager, TransactionManager>();
            //services.AddSingleton<ITransactionalDatabase, TransactionalDatabase>();
            services.AddTransient(provider => provider.GetRequiredService<ITransactionalDatabase>().CreateScope());

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
                //builder.AddFile("app.log");
            });
        }
    }

    public sealed class BankAccount
    {
        public long Id { get; set; }
        public int Amount { get; set; }
    }
}
