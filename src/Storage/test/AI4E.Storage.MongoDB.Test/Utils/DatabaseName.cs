using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace AI4E.Storage.MongoDB.Test.Utils
{
    internal static class DatabaseName
    {
        private const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private static readonly ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random());

        public static string GenerateRandom()
        {
            var databaseName = new string(default, count: 32);
            var databaseNameMem = MemoryMarshal.AsMemory(databaseName.AsMemory());

            for (var i = 0; i < databaseNameMem.Length; i++)
            {
                databaseNameMem.Span[i] = chars[random.Value.Next(chars.Length)];
            }

            return databaseName;
        }
    }
}
