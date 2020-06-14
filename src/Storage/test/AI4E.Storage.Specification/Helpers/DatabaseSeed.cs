using System.Threading.Tasks;
using AI4E.Storage.Specification.TestTypes;

#pragma warning disable CA2007

namespace AI4E.Storage.Specification.Helpers
{
    internal static class DatabaseSeed
    {
        public static async Task SeedDatabaseAsync(IDatabase database)
        {
            var valueSum = 0;

            for (var i = 1; i <= 10; i++)
            {
                var entry = new MinimalEntry { Id = i, Value = 1 };
                await database.AddAsync(entry, cancellation: default);
                valueSum += entry.Value;
            }

            await database.AddAsync(new ValueSumEntry { Id = 1, ValueSum = valueSum }, cancellation: default);
        }
    }
}
