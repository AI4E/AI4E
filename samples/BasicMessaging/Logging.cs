using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BasicMessaging
{
    class Logging : ILogToConsole
    {
        public Task LogAsync(string text)
        {
            return Console.Out.WriteLineAsync(text);
        }
    }
}
